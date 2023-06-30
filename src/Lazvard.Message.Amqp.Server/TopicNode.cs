using Microsoft.Azure.Amqp;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Lazvard.Message.Amqp.Server;

sealed class TopicNode : Node
{
    private readonly SubscriptionHandler subscriptionHandler;
    private readonly ILoggerFactory loggerFactory;
    private readonly ConcurrentDictionary<Guid, Publisher> publishers;

    public TopicNode(TopicConfig config, SubscriptionHandler subscriptionHandler, ILoggerFactory loggerFactory) : base(config.Name)
    {
        this.subscriptionHandler = subscriptionHandler;
        this.loggerFactory = loggerFactory;
        publishers = new(2, 5);

    }

    public override void OnAttachReceivingLink(ReceivingAmqpLink link)
    {
        var id = Guid.NewGuid();

        if (publishers.TryAdd(id, new Publisher(link, subscriptionHandler, loggerFactory)))
        {
            link.Closed += new EventHandler((s, e) => publishers.TryRemove(id, out _));
        }
    }

    public override void OnAttachSendingLink(SendingAmqpLink link)
    {
        subscriptionHandler.OnAttachSendingLink(link);
    }
}
