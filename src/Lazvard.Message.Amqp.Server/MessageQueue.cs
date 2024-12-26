﻿using Lazvard.Message.Amqp.Server.Constants;
using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.Azure.Amqp;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Lazvard.Message.Amqp.Server;


public interface IMessageQueue
{
    bool TryDefer(Guid lockToken, string linkName);
    Result<AmqpMessage> TryLock(AmqpMessage message, string linkName);
    bool TryRelease(Guid lockToken, string linkName);
    bool TryRemove(AmqpMessage message);
    bool TryRemove(Guid lockToken, string linkName);
    Result<DateTime> TryRenewLock(Guid lockToken, string linkName);
    ValueTask<Result<AmqpMessage>> DequeueAsync(CancellationToken cancellationToken = default);
    IEnumerable<AmqpMessage> GetDeferredMessages(long[] messageSeqNo);
    IEnumerable<AmqpMessage> Peek(int maxMessages, long? fromSequenceNumber);
    long Enqueue(AmqpMessage message);
    bool TryDeadletter(Guid lockToken, string linkName);
    bool TryDeadletter(AmqpMessage message);
    bool TryReEnqueue(AmqpMessage message);
}



public sealed class MessageQueue : IMessageQueue, IDisposable
{
    private readonly ConcurrentDictionary<long, BrokerMessage> items;
    private readonly ConcurrentQueue<BrokerMessage> sendQueue;
    private readonly SemaphoreSlim semaphore;
    private readonly IExpirationList expirationList;
    private readonly ILogger<MessageQueue> logger;
    private readonly TopicSubscriptionConfig config;
    private readonly IMessageQueue? deadletterQueue;
    private long sequenceNo = 0;

    public MessageQueue(TopicSubscriptionConfig topicSubscriptionConfig, CancellationToken stopToken, IMessageQueue? deadletterQueue, ILoggerFactory loggerFactory)
    {
        items = new();
        sendQueue = new();
        semaphore = new(1);
        config = topicSubscriptionConfig;
        expirationList = new ExpirationList(config.LockDuration.ToTimeSpan() / 2, OnLockExpiration, stopToken);
        logger = loggerFactory.CreateLogger<MessageQueue>();
        this.deadletterQueue = deadletterQueue;
    }

    private void OnLockExpiration(BrokerMessage message)
    {
        logger.LogTrace("message {MessageSeqNo} in subscription {Subscription} with lock {LockId} expired and requeued",
            message.TraceId, config.FullName, message.LockToken);

        Replace(message.Unlock());

        if (message.IsDeferred)
        {
            return;
        }

        // when message is expired, we should increase the delivery count
        if (MovedToDeadletterAfterIncreaseDeliveryCount(message))
        {
            return;
        }

        TryReEnqueue(message.Message);
    }
    public long Enqueue(AmqpMessage message)
    {
        var clonedMessage = message.Clone(true);

        var messageSeqNo = Interlocked.Increment(ref sequenceNo);

        clonedMessage.MessageAnnotations.Map[AmqpMessageConstants.SequenceNumber] = messageSeqNo;
        clonedMessage.MessageAnnotations.Map[AmqpMessageConstants.EnqueueSequenceNumber] = messageSeqNo;
        clonedMessage.MessageAnnotations.Map[AmqpMessageConstants.EnqueuedTime] = DateTime.UtcNow;
        clonedMessage.MessageAnnotations.Map[AmqpMessageConstants.MessageState] = (int)MessageState.Active;

        clonedMessage.Header.DeliveryCount ??= 0;
        clonedMessage.Properties.MessageId ??= Guid.NewGuid();
        clonedMessage.Properties.AbsoluteExpiryTime = DateTime.UtcNow + config.TimeToLive;
        clonedMessage.Properties.CreationTime = DateTime.UtcNow;
        clonedMessage.Header.Ttl = Convert.ToUInt32(config.TimeToLive.ToTimeSpan().TotalSeconds);

        var brokerMessage = new BrokerMessage(clonedMessage);
        items.TryAdd(messageSeqNo, brokerMessage);
        sendQueue.Enqueue(brokerMessage);

        semaphore.Release();

        return messageSeqNo;
    }

    public bool TryReEnqueue(AmqpMessage message)
    {
        if (items.TryGetValue(message.GetSequenceNumber(), out var value))
        {
            sendQueue.Enqueue(value);
            semaphore.Release();

            return true;
        }

        return false;
    }

    public Result<AmqpMessage> TryLock(AmqpMessage message, string linkName)
    {
        if (!items.TryGetValue(message.GetSequenceNumber(), out var brokerMessage))
        {
            return Result.Fail();
        }

        return TryLock(brokerMessage, linkName);
    }

    public Result<AmqpMessage> TryLock(BrokerMessage message, string linkName)
    {
        var lockedUntil = DateTime.UtcNow + config.LockDuration;
        var lockToken = Guid.NewGuid();
        var lockedMessage = message.Lock(lockToken, lockedUntil, linkName);

        if (!expirationList.TryAdd(lockedMessage))
            return Result.Fail();

        Replace(lockedMessage);

        var clonedMessage = lockedMessage.Message.Clone(true);
        clonedMessage.DeliveryTag = new ArraySegment<byte>(lockToken.ToByteArray());
        clonedMessage.MessageAnnotations.Map[AmqpMessageConstants.LockedUntil] = lockedUntil;
        clonedMessage.DeliveryAnnotations.Map[AmqpMessageConstants.LockToken] = lockToken;

        logger.LogTrace("add lock {LockId} until {LockedUntil} for message {MessageSeqNo} in subscription {Subscription}",
            lockToken, lockedUntil.Ticks, message.TraceId, config.FullName);

        return clonedMessage;
    }

    public Result<DateTime> TryRenewLock(Guid lockToken, string linkName)
    {
        var message = expirationList.TryRemove(lockToken, linkName);
        if (!message.IsSuccess) return Result.Fail();

        var lockedUntil = DateTime.UtcNow + config.LockDuration;
        var lockedMessage = message.Value.RenewLock(lockedUntil);

        if (!expirationList.TryAdd(lockedMessage))
            return Result.Fail();

        Replace(lockedMessage);

        return lockedUntil;
    }

    /// <summary>
    /// Unload and move the message to deadletter q
    /// </summary>
    /// <param name="lockToken"></param>
    /// <param name="linkName"></param>
    /// <returns></returns>
    public bool TryDeadletter(Guid lockToken, string linkName)
    {
        if (deadletterQueue is null)
        {
            return false;
        }

        var message = expirationList.TryRemove(lockToken, linkName);
        if (!message.IsSuccess) return false;

        var removed = items.TryRemove(message.Value.Message.GetSequenceNumber(), out _);
        if (!removed) return false;

        deadletterQueue.Enqueue(message.Value.Message);

        return true;
    }

    public bool TryDeadletter(AmqpMessage message)
    {
        if (deadletterQueue is null)
        {
            return false;
        }

        var removed = items.TryRemove(message.GetSequenceNumber(), out _);
        if (!removed) return false;

        deadletterQueue.Enqueue(message);

        return true;
    }

    public bool TryRelease(Guid lockToken, string linkName)
    {
        var message = expirationList.TryRemove(lockToken, linkName);
        if (!message.IsSuccess) return false;

        var releasedMessage = message.Value.Unlock();
        Replace(releasedMessage);

        if (releasedMessage.IsDeferred)
        {
            return true;
        }

        // put it in the send queue again so it will send to the consumers again
        if (MovedToDeadletterAfterIncreaseDeliveryCount(message.Value))
        {
            return true;
        }

        sendQueue.Enqueue(message.Value);
        semaphore.Release();
        return true;
    }

    /// <summary>
    /// release the lock and remove the message from the queue
    /// </summary>
    /// <param name="lockToken"></param>
    /// <returns></returns>
    public bool TryRemove(Guid lockToken, string linkName)
    {
        var message = expirationList.TryRemove(lockToken, linkName);
        if (!message.IsSuccess) return false;

        return items.TryRemove(message.Value.Message.GetSequenceNumber(), out _);
    }

    public bool TryRemove(AmqpMessage message)
    {
        return items.TryRemove(message.GetSequenceNumber(), out _);
    }

    /// <summary>
    /// Unload and defer message
    /// </summary>
    /// <param name="lockToken"></param>
    /// <param name="linkName"></param>
    /// <returns></returns>
    public bool TryDefer(Guid lockToken, string linkName)
    {
        var message = expirationList.TryRemove(lockToken, linkName);
        if (!message.IsSuccess) return false;

        var deferredMessage = message.Value.Defer();
        deferredMessage.Message.MessageAnnotations.Map[AmqpMessageConstants.MessageState] = (int)MessageState.Deferred;

        Replace(message.Value.Defer());
        return true;
    }

    public async ValueTask<Result<AmqpMessage>> DequeueAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (sendQueue.TryDequeue(out var message))
            {
                return message.Message;
            }

            await semaphore.WaitAsync(cancellationToken);
        }

        return Result.Fail();
    }

    public IEnumerable<AmqpMessage> GetDeferredMessages(long[] messageSeqNos)
    {
        foreach (var messageSeqNo in messageSeqNos)
        {
            if (items.TryGetValue(messageSeqNo, out var result) && result.IsDeferred && !result.IsLocked)
            {
                yield return result.Message;
            }
        }
    }


    public IEnumerable<AmqpMessage> Peek(int maxMessages, long? fromSequenceNumber)
    {
        // TODO: replace the items data type from ConcurrentDictionary to a more suitable data structure
        // maybe BTree+ ? I couldn't find any implementation of lock free BTree+ for c#
        return items.OrderBy(x => x.Key)
            .Where(x => !fromSequenceNumber.HasValue || x.Key > fromSequenceNumber.Value)
            .Select(x => x.Value.Message).Take(maxMessages);
    }

    private void Replace(BrokerMessage message)
    {
        var sequenceNumber = message.Message.GetSequenceNumber();
        items[sequenceNumber] = message;
    }

    public void Dispose()
    {
        semaphore.Dispose();
    }

    private bool MovedToDeadletterAfterIncreaseDeliveryCount(BrokerMessage message)
    {
        message.Message.IncreaseDeliveryCount();
        logger.LogTrace("increase delivery count to {DeliveryCount} for message {MessageSeqNo} in subscription {Subscription}",
           message.Message.Header.DeliveryCount, message.TraceId, config.FullName);

        if (message.Message.Header.DeliveryCount >= config.MaxDeliveryCount)
        {
            logger.LogError("message {MessageSeqNo} in subscription {Subscription} has reached the maximum delivery count {MaxDeliveryCount} and will be dead-lettered",
              message.TraceId, config.FullName, config.MaxDeliveryCount);

            if (!TryDeadletter(message.Message))
            {
                logger.LogError("can not move message {MessageSeqNo} in subscription {Subscription} to dead-lettered",
                    message.TraceId, config.FullName);

                return false;
            }

            return true;
        }

        return false;
    }
}
