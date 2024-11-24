using Lazvard.Message.Amqp.Server.Constants;
using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.Azure.Amqp;
using Microsoft.Extensions.Logging;

namespace Lazvard.Message.Amqp.Server;

public sealed class ManagementSubscription : SubscriptionBase
{
    private readonly IMessageQueue subjectMessageQueue;

    public ManagementSubscription(
        IMessageQueue subjectMessageQueue,
        TopicSubscriptionConfig config,
        IMessageQueue messageQueue,
        ConsumerFactory consumerFactory,
        ILoggerFactory loggerFactory,
        CancellationToken stopToken) :
        base(config, messageQueue, consumerFactory, loggerFactory, stopToken)
    {
        this.subjectMessageQueue = subjectMessageQueue;
    }

    protected override void ProcessIncomingMessage(AmqpMessage message, CancellationToken stopToken)
    {
        if (!message.GetAppProperties(ManagementConstants.Request.Operation, out var operation))
        {
            logger.LogWarning("can't parse the management message");
            DeliverArgumentErrorResponse(message, "required `operation` parameters is missing");
            return;
        }

        ProcessOperation(operation, message);
    }

    private void ProcessOperation(string operation, AmqpMessage message)
    {
        logger.LogTrace("process management operation {Operation}", operation);

        switch (operation)
        {
            case ManagementConstants.Operations.RenewLockOperation:
                RenewLock(message);
                break;
            case ManagementConstants.Operations.ReceiveBySequenceNumberOperation:
                ReceiveBySequenceNumber(message);
                break;
            case ManagementConstants.Operations.UpdateDispositionOperation:
                UpdateDispositionOperation(message);
                break;
            case ManagementConstants.Operations.PeekMessageOperation:
                PeekMessages(message);
                break;
            default:
                DeliverArgumentErrorResponse(message, $"The operation `{operation}` is invalid.");
                break;
        }
    }

    private bool TryHandleDisposition(string status, Guid lockToken, string linkName)
    {
        return status switch
        {
            ManagementConstants.DispositionStatus.Completed => subjectMessageQueue.TryRemove(lockToken, linkName),
            ManagementConstants.DispositionStatus.Suspended => subjectMessageQueue.TryDeadletter(lockToken, linkName),
            ManagementConstants.DispositionStatus.Abandoned => subjectMessageQueue.TryRelease(lockToken, linkName),
            ManagementConstants.DispositionStatus.Defered => subjectMessageQueue.TryDefer(lockToken, linkName),
            _ => false,
        };
    }

    private void UpdateDispositionOperation(AmqpMessage message)
    {
        var lockTokens = message.ReadAsMap<Guid[]>(ManagementConstants.Properties.LockTokens);
        var status = message.ReadAsMap<string>(ManagementConstants.Properties.DispositionStatus);

        if (!message.GetAppProperties(ManagementConstants.Request.AssociatedLinkName, out var linkName)
            || !lockTokens.IsSuccess
            || !status.IsSuccess)
        {
            DeliverArgumentErrorResponse(message, "required parameters is missing");
            return;
        }

        foreach (var lockToken in lockTokens.Value)
        {
            TryHandleDisposition(status.Value, lockToken, linkName);
        }

        var reply = ResponseMessageBuilder.Success()
           .ReplyTo(message)
           .Build();

        DeliverMessage(reply);
    }

    private void ReceiveBySequenceNumber(AmqpMessage message)
    {
        var sequenceNumbers = message.ReadAsMap<long[]>(ManagementConstants.Properties.SequenceNumbers);
        var settleMode = message.ReadAsMap<uint>(ManagementConstants.Properties.ReceiverSettleMode);
        var hasLinkName = message.GetAppProperties(ManagementConstants.Request.AssociatedLinkName, out var linkName);
        //var sessionId = ReadFromMap<string>(message, ManagementConstants.Properties.SessionId);

        if (!sequenceNumbers.IsSuccess
            || !settleMode.IsSuccess
            || (!hasLinkName && (SettleMode)settleMode.Value == SettleMode.SettleOnReceive))
        {
            DeliverArgumentErrorResponse(message, "required parameters is missing");
            return;
        }

        var deferredMessages = subjectMessageQueue.GetDeferredMessages(sequenceNumbers.Value);
        var messages = SettleMessages(deferredMessages, linkName, (SettleMode)settleMode.Value).ToArray();
        if (messages.Count() != sequenceNumbers.Value.Length)
        {
            DeliverMessage(ResponseMessageBuilder.Failed(AmqpResponseStatusCode.BadRequest)
                .WithError(ManagementConstants.Errors.MessageNotFoundError, "can not find messages or messages are not deferred")
                .ReplyTo(message)
                .Build());
            return;
        }

        var reply = ResponseMessageBuilder.Success()
            .WithMessages(messages)
            .ReplyTo(message)
            .Build();

        DeliverMessage(reply);
    }

    private IEnumerable<AmqpMessage> SettleMessages(
        IEnumerable<AmqpMessage> messages,
        string? linkName,
        SettleMode settleMode)
    {
        foreach (var message in messages)
        {
            if (settleMode == SettleMode.SettleOnSend && subjectMessageQueue.TryRemove(message))
            {
                yield return message;
            }
            else if (settleMode == SettleMode.SettleOnReceive && linkName is not null)
            {
                var lockResult = subjectMessageQueue.TryLock(message, linkName);

                if (lockResult.IsSuccess)
                    yield return lockResult.Value;
            }
        }
    }

    private void PeekMessages(AmqpMessage message)
    {
        var fromSequenceNumber = message.ReadAsMap<long>(ManagementConstants.Properties.FromSequenceNumber);
        var messageCount = message.ReadAsMap<int>(ManagementConstants.Properties.MessageCount);

        if (!fromSequenceNumber.IsSuccess || !messageCount.IsSuccess)
        {
            DeliverArgumentErrorResponse(message, "required parameters are missing");
            return;
        }

        var messages = subjectMessageQueue.Peek(messageCount.Value, fromSequenceNumber.Value);

        DeliverMessage(ResponseMessageBuilder.Success()
            .WithMessages(messages)
            .ReplyTo(message)
            .Build());
    }

    private void DeliverArgumentErrorResponse(AmqpMessage message, string statusDescription)
    {
        DeliverMessage(ResponseMessageBuilder.Failed(AmqpResponseStatusCode.BadRequest)
            .WithError(ManagementConstants.Errors.ArgumentError, statusDescription)
            .ReplyTo(message)
            .Build());
    }

    private void RenewLock(AmqpMessage message)
    {
        var (isMessageParsed, lockTokens) = message.ReadAsMap<Guid[]>(ManagementConstants.Properties.LockTokens);

        if (!message.GetAppProperties(ManagementConstants.Request.AssociatedLinkName, out var linkName)
            || !isMessageParsed)
        {
            DeliverArgumentErrorResponse(message, "required parameters is missing");
            return;
        }


        logger.LogTrace("renewing lock with token {LockToken} in link {Link}", lockTokens, linkName);

        var (isLockRenewed, lockedUntil) = subjectMessageQueue.TryRenewLock(lockTokens[0], linkName);
        if (isLockRenewed)
        {
            logger.LogTrace("lock with token {LockToken} in link {Link} renewed", lockTokens, linkName);

            var reply = ResponseMessageBuilder.Success()
                .WithProperty(ManagementConstants.Properties.Expirations, new[] { lockedUntil })
                .ReplyTo(message)
                .Build();

            DeliverMessage(reply);
        }
        else
        {
            logger.LogTrace("renewing lock in link {Link} failed", linkName);

            var reply = ResponseMessageBuilder.Failed(AmqpResponseStatusCode.Forbidden)
                .WithError(ManagementConstants.Errors.MessageLockLostError, "The lock supplied is invalid. Either the lock expired, or the message has already been removed from the queue, or was received by a different receiver instance.")
                .ReplyTo(message)
                .Build();

            DeliverMessage(reply);
        }
    }

    private void DeliverMessage(AmqpMessage message)
    {
        var consumer = GetTargetConsumer(message);
        if (consumer.IsSuccess)
        {
            var delivered = consumer.Value.TryToDeliver(message);

            logger.LogTrace("delivering message {MessageSeqNo} in subscription {Subscription} to consumer {Link} was {Status}",
             message.GetSequenceNumber(), fullName, consumer.Value.LinkName, delivered ? "Successful" : "Failed");

            if (delivered) return;
        }

        logger.LogError("delivering message {MessageSeqNo} in subscription {Subscription} Failed",
            message.GetSequenceNumber(), fullName);
    }

    private Result<Consumer> GetTargetConsumer(AmqpMessage message)
    {
        if (message.Properties?.To != null)
        {
            if (consumers.TryGetValue(message.Properties.To.ToString() ?? "", out var consumer) && !consumer.IsDrain)
            {
                return consumer;
            }

            logger.LogError("the message {MessageSeqNo} receiver {Receiver} is drain",
                  message.GetTraceId(), message.Properties?.To);
        }
        else
        {
            logger.LogError("the message {MessageSeqNo} receiver is not specified", message.GetTraceId());
        }

        return Result.Fail();
    }
}