using Lazvard.Message.Amqp.Server.Constants;
using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Lazvard.Message.Amqp.Server;

public class SubscriptionHandler
{
    readonly ConcurrentDictionary<string, ISubscription> subscriptions;
    readonly ConcurrentDictionary<string, ISubscription> subscriptionLinks;

    private readonly ILogger logger;

    public SubscriptionHandler(
        IEnumerable<ISubscription> subscriptions,
        ILoggerFactory loggerFactory)
    {
        subscriptionLinks = new(2, 2);
        this.subscriptions = new(
            subscriptions
                .Select(s => KeyValuePair.Create(s.Name.ToLowerInvariant(), s))
        );

        logger = loggerFactory.CreateLogger<SubscriptionHandler>();
    }

    public IEnumerable<ISubscription> GetMessageSubscription() =>
        subscriptions
        .Where(x => !x.Value.Name.EndsWith(SubscriptionConstants.Management))
        .Where(x => !x.Value.Name.EndsWith(SubscriptionConstants.DeadletterQueue))
        .Select(x => x.Value);

    public ISubscription? GetSubscriptionByAddress(Address address) =>
        subscriptions
            .FirstOrDefault(x => x.Value.HasAddress(address))
            .Value;

    public void OnAttachSendingLink(SendingAmqpLink link)
    {
        var address = ((Source)link.Settings.Source).Address;

        logger.LogTrace("attach sending link from source {Address} and link {Link} with Identifier {Identifier}",
            address, link.Name, link.Identifier);


        if (address is null)
        {
            link.SafeClose(new AmqpException(AmqpErrorCode.InternalError, "The subscription address is not valid."));
            return;
        }

        var subscriptionName = AddressParser.Parse(address).Subscription?.ToLowerInvariant() ?? "";

        if (!subscriptions.ContainsKey(subscriptionName))
        {
            logger.LogWarning("can not find the subscription {SubscriptionName}", subscriptionName);
            link.SafeClose(new AmqpException(AmqpErrorCode.InternalError, "Can not find the subscription."));
            return;
        }

        // in order to avoid race conditions, ContainsKey can return false 
        // for two parallel thread
        if (subscriptions.TryGetValue(subscriptionName, out var subscription))
        {

            subscription.OnAttachSendingLink(link);
            subscriptionLinks.TryAdd(link.Name, subscription);
        }
    }
}
