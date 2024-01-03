using Lazvard.Message.Amqp.Server.Constants;
using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Logging;

namespace Lazvard.Message.Amqp.Server;

public sealed class Consumer
{
    private readonly ILogger<Consumer> logger;
    private readonly SendingAmqpLink link;
    private readonly IMessageQueue messageQueue;
    private readonly Action runDeliverMessages;

    private SettleMode settleType => link.Settings.SettleType;

    // the offset to last received message
    private uint deliveryTag;
    private uint credit;

    public bool IsDrain { get; private set; }
    public int ReceivedMessages { get; private set; }
    public string LinkName => link.Name;

    public Consumer(
        SendingAmqpLink link,
        IMessageQueue messageQueue,
        Action runDeliverMessages,
        ILoggerFactory loggerFactory)
    {
        IsDrain = true;

        logger = loggerFactory.CreateLogger<Consumer>();
        this.link = link;
        this.messageQueue = messageQueue;
        this.runDeliverMessages = runDeliverMessages;
        this.link.RegisterCreditListener(OnCredit);
        this.link.RegisterDispositionListener(OnDisposition);
    }

    private void OnDisposition(Delivery delivery)
    {
        ProcessDisposition(delivery);
    }

    private bool TryDisposeMessage(Guid lockToken, DeliveryState state)
    {
        if (state.DescriptorCode == Accepted.Code)
        {
            return messageQueue.TryRemove(lockToken, link.Name);
        }
        if (state.DescriptorCode == Rejected.Code)
        {
            return messageQueue.TryDeadletter(lockToken, link.Name);
        }
        if (state.DescriptorCode == Modified.Code && state is Modified modified)
        {
            if (modified.UndeliverableHere == true)
            {
                return messageQueue.TryDefer(lockToken, link.Name);
            }
            else
            {
                return messageQueue.TryRelease(lockToken, link.Name);
            }
        }

        return false;
    }

    private void ProcessDisposition(Delivery delivery)
    {
        if (delivery.DeliveryTag.Array is null || delivery.DeliveryTag.Array.Length != 16)
        {
            logger.LogWarning("invalid delivery tag received in disposition {DeliveryTag}", delivery.DeliveryTag);
            link.DisposeDelivery(delivery, true, new Accepted());
            return;
        }

        var lockToken = new Guid(delivery.DeliveryTag.Array);
        logger.LogTrace("process delivery tag in disposition {DeliveryTag} with status {State}",
            lockToken, delivery.State.DescriptorName);


        if (!TryDisposeMessage(lockToken, delivery.State))
        {
            logger.LogTrace("process delivery tag in disposition {DeliveryTag} was failed",
                lockToken);

            var state = new Rejected
            {
                Error = new Error() { Condition = ManagementConstants.Errors.MessageLockLostError }
            };

            link.DisposeDelivery(delivery, false, state);
            return;
        }

        logger.LogTrace("process delivery tag in disposition {DeliveryTag} was successful",
            lockToken);

        link.DisposeDelivery(delivery, true, new Accepted());
    }

    private void OnCredit(uint credit, bool drain, ArraySegment<byte> txnId)
    {
        logger.LogTrace("process credit {credit} for link {Link}",
            credit, link.Name);

        var oldIsDrain = IsDrain;

        this.credit = credit;
        IsDrain = drain;

        if (oldIsDrain && !IsDrain)
        {
            runDeliverMessages();
        }
    }

    private ArraySegment<byte> GetNextTag()
    {
        return new ArraySegment<byte>(BitConverter.GetBytes(Interlocked.Increment(ref deliveryTag)));
    }

    public bool TryToDeliver(AmqpMessage message)
    {
        logger.LogTrace("trying to deliver message {MessageSeqNo} to {Link}",
            message.GetSequenceNumber(), LinkName);

        if (credit <= 0)
        {
            logger.LogTrace("consumer {Link} is drain", LinkName);

            return false;
        }

        var clonedMessage = AddDeliveryTag(message);
        if (!clonedMessage.IsSuccess) return false;

        ReceivedMessages++;

        try
        {
            link.SendMessageNoWait(clonedMessage.Value, clonedMessage.Value.DeliveryTag, new ArraySegment<byte>());
            message.Header.DeliveryCount += 1;

            logger.LogTrace("delivered message {MessageSeqNo} to {Link} with DeliveryTag {DeliveryTag}, DeliveryCount {DeliveryCount}",
                clonedMessage.Value.GetSequenceNumber(), LinkName, clonedMessage.Value.DeliveryTag, clonedMessage.Value.Header.DeliveryCount ?? 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "error in delivering message {MessageSeqNo} to {Link}",
                 message.GetSequenceNumber(), LinkName);

            if (settleType == SettleMode.SettleOnDispose
                && message.DeliveryTag.Array != null
                && messageQueue.TryRemove(new Guid(message.DeliveryTag.Array), link.Name))
            {
                // reprocess message right away 
                return false;
            }
        }

        return true;
    }

    private Result<AmqpMessage> AddDeliveryTag(AmqpMessage message)
    {
        if (settleType == SettleMode.SettleOnDispose)
        {
            var lockedMessage = messageQueue.TryLock(message, link.Name);
            if (!lockedMessage.IsSuccess)
            {
                return Result.Fail();
            }

            return lockedMessage.Value;
        }
        else
        {
            var clonedMessage = message.Clone(true);
            clonedMessage.DeliveryTag = GetNextTag();

            return clonedMessage;
        }
    }
}