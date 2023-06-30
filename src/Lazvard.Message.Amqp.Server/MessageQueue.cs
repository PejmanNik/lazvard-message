using Iso8601DurationHelper;
using Lazvard.Message.Amqp.Server.Constants;
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
    private readonly Duration lockDuration;
    private readonly IMessageQueue? deadletterQueue;
    private long sequenceNo = 0;

    public MessageQueue(Duration lockDuration, CancellationToken stopToken, IMessageQueue? deadletterQueue, ILoggerFactory loggerFactory)
    {
        items = new();
        sendQueue = new();
        semaphore = new(1);
        expirationList = new ExpirationList(lockDuration.ToTimeSpan() / 2, OnLockExpiration, stopToken);
        logger = loggerFactory.CreateLogger<MessageQueue>();
        this.lockDuration = lockDuration;
        this.deadletterQueue = deadletterQueue;
    }

    private void OnLockExpiration(BrokerMessage message)
    {
        logger.LogTrace("message {MessageSeqNo} with lock {LockId} expired and requeued",
            message.SequenceNumber, message.LockToken);

        Replace(message.Unlock());

        if (!message.IsDeferred)
        {
            TryReEnqueue(message.Message);
        }
    }

    public long Enqueue(AmqpMessage message)
    {
        var clonedMessage = message.Clone(true);

        var messageSeqNo = Interlocked.Increment(ref sequenceNo);

        clonedMessage.MessageAnnotations ??= new();
        clonedMessage.MessageAnnotations.Map[AmqpMessageConstants.SequenceNumberName] = messageSeqNo;

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
        var lockedUntil = DateTime.UtcNow + lockDuration;
        var lockToken = Guid.NewGuid();
        var lockedMessage = message.Lock(lockToken, lockedUntil, linkName);

        if (!expirationList.TryAdd(lockedMessage))
            return Result.Fail();

        Replace(lockedMessage);

        var clonedMessage = lockedMessage.Message.Clone(true);
        clonedMessage.DeliveryTag = new ArraySegment<byte>(lockToken.ToByteArray());
        clonedMessage.MessageAnnotations.Map[AmqpMessageConstants.LockedUntilName] = lockedUntil;

        logger.LogTrace("add lock {LockId} until {LockedUntil} for message {MessageSeqNo}",
            lockToken, lockedUntil.Ticks, message.SequenceNumber);

        return clonedMessage;
    }

    public Result<DateTime> TryRenewLock(Guid lockToken, string linkName)
    {
        var message = expirationList.TryRemove(lockToken, linkName);
        if (!message.IsSuccess) return Result.Fail();

        var lockedUntil = DateTime.UtcNow + lockDuration;
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

        if (!releasedMessage.IsDeferred)
        {
            // put it in the send queue again so it will send to the consumers again
            sendQueue.Enqueue(message.Value);
            semaphore.Release();
        }

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
        items[message.SequenceNumber] = message;
    }

    public void Dispose()
    {
        semaphore.Dispose();
    }

}
