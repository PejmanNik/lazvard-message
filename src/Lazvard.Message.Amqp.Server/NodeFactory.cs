using Lazvard.Message.Amqp.Server.Constants;
using Microsoft.Extensions.Logging;

namespace Lazvard.Message.Amqp.Server;

public class NodeFactory
{
    private readonly ILoggerFactory loggerFactory;
    private readonly CancellationToken stopToken;

    public NodeFactory(ILoggerFactory loggerFactory, CancellationToken stopToken)
    {
        this.loggerFactory = loggerFactory;
        this.stopToken = stopToken;
    }

    protected virtual IEnumerable<ISubscription> CreateSubscriptions(TopicSubscriptionConfig config)
    {
        var consumerFactory = new ConsumerFactory(loggerFactory);

        var deadletterConfig = new TopicSubscriptionConfig($"{config.Name}/{SubscriptionConstants.DeadletterQueue}");
        var deadletterMessageQueue = new MessageQueue(config, stopToken, null, loggerFactory);
        var deadletterQueue = new Subscription(deadletterConfig, deadletterMessageQueue, consumerFactory, loggerFactory, stopToken);
        yield return deadletterQueue;

        var messageQueue = new MessageQueue(config, stopToken, deadletterMessageQueue, loggerFactory);
        var subscription = new Subscription(config, messageQueue, consumerFactory, loggerFactory, stopToken);
        yield return subscription;

        var managementConfig = new TopicSubscriptionConfig($"{config.Name}/{SubscriptionConstants.Management}");
        yield return new ManagementSubscription(
            messageQueue,
            managementConfig,
            new MessageQueue(config, stopToken, null, loggerFactory),
            consumerFactory,
            loggerFactory,
            stopToken);
    }

    protected virtual INode CreateNode(TopicConfig config)
    {
        var subscriptions = config.Subscriptions.SelectMany(CreateSubscriptions);
        var subscriptionHandler = new SubscriptionHandler(subscriptions, loggerFactory);

        return new TopicNode(config, subscriptionHandler, loggerFactory);
    }

    public virtual IEnumerable<INode> Create(BrokerConfig config)
    {
        return config.Topics.Select(CreateNode)
            .Append(new CbsNode(loggerFactory));
    }
}
