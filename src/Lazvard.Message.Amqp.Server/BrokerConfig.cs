using Iso8601DurationHelper;
using Microsoft.Azure.Amqp;

namespace Lazvard.Message.Amqp.Server;

public class TopicSubscriptionConfig
{
    public TopicSubscriptionConfig(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    public string TopicName { get; private set; } = string.Empty;
    public Duration LockDuration { get; set; } = Duration.FromMinutes(1);
    public int MaxDeliveryCount { get; set; } = 50;
    public Duration TimeToLive { get; set; } = Duration.FromDays(14);

    public string FullName => $"{TopicName}/{Name}";

    internal TopicSubscriptionConfig SetTopicName(string name)
    {
        TopicName = name;
        return this;
    }
}


public class TopicConfig
{
    public TopicConfig(string name, IEnumerable<TopicSubscriptionConfig> subscriptions)
    {
        Name = name;
        Subscriptions = subscriptions.Select(x=> x.SetTopicName(name));
    }

    public string Name { get; set; }
    public IEnumerable<TopicSubscriptionConfig> Subscriptions { get; set; }

}

public class BrokerConfig
{
    public int Port { get; set; } = AmqpConstants.DefaultSecurePort;
    public string IP { get; set; } = "localhost";
    public uint MaxFrameSize { get; set; } = 64 * 1024;
    public uint ConnectionIdleTimeOut { get; set; } = 4 * 60 * 1000;
    public uint MaxMessageSize { get; set; } = 64 * 1024 * 1024;

    public IEnumerable<TopicConfig> Topics { get; set; } = Array.Empty<TopicConfig>();
}
