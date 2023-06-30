using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Lazvard.Message.Amqp.Server;

public sealed class CbsNode : Node
{
    private readonly ILogger<CbsNode> logger;
    private readonly ConcurrentDictionary<Address, SendingAmqpLink> senders;

    public CbsNode(ILoggerFactory loggerFactory) : base(Constants.CbsConstants.CbsAddress)
    {
        logger = loggerFactory.CreateLogger<CbsNode>();
        senders = new ConcurrentDictionary<Address, SendingAmqpLink>(10, 20);
    }

    public override void OnAttachReceivingLink(ReceivingAmqpLink link)
    {
        link.RegisterMessageListener(OnMessage);
        link.SetTotalLinkCredit(100u, true, true);
    }

    public override void OnAttachSendingLink(SendingAmqpLink link)
    {
        Address replyTo = link.Settings.Address(false) ?? link.Settings.LinkName;
        if (senders.TryAdd(replyTo, link))
        {
            link.SafeAddClosed(OnLinkClose);
        }
        else
        {
            link.SafeClose(new AmqpException(AmqpErrorCode.NotAllowed,
                $"A link with return target address '{replyTo}' already exists on node '{Name}'"));
        }
    }

    void OnLinkClose(object? sender, EventArgs args)
    {
        if (sender is SendingAmqpLink link)
        {
            var replyTo = link.Settings.Address(false) ?? link.Settings.LinkName;
            senders.TryRemove(replyTo, out _);
        }
    }

    void OnMessage(AmqpMessage message)
    {
        var replayTo = message.Properties?.ReplyTo;
        if (replayTo == null || !senders.TryGetValue(replayTo, out var sender))
        {
            logger.LogError("can't find the replayTo link with address {ReplyTo} in {Name}", replayTo, Name);
            return;
        }

        var response = AmqpMessage.Create();
        response.ApplicationProperties.Map[Constants.CbsConstants.PutToken.StatusCode] = 200;
        response.Properties.CorrelationId = message.Properties?.MessageId;
        response.Settled = true;

        sender.SendMessageNoWait(response, AmqpConstants.EmptyBinary, AmqpConstants.NullBinary);
    }
}