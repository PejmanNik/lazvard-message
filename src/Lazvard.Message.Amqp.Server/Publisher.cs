using Microsoft.Azure.Amqp;
using Microsoft.Extensions.Logging;

namespace Lazvard.Message.Amqp.Server;

public sealed class Publisher
{
    private readonly ReceivingAmqpLink link;
    private readonly SubscriptionHandler subscriptionHandler;
    private readonly ILogger logger;

    public Publisher(ReceivingAmqpLink link, SubscriptionHandler subscriptionHandler, ILoggerFactory loggerFactory)
    {
        this.link = link;
        this.subscriptionHandler = subscriptionHandler;
        logger = loggerFactory.CreateLogger<Publisher>();

        this.link.RegisterMessageListener(OnMessage);
    }

    private void OnMessage(AmqpMessage message)
    {
        if (message.Properties?.ReplyTo != null)
        {
            var subscription = subscriptionHandler
                .GetSubscriptionByAddress(message.Properties.ReplyTo);

            if (subscription == null)
            {
                link.RejectMessage(message, new AmqpException(AmqpErrorCode.NotAllowed,
                    $"Can't find the reply link with address '{message.Properties.ReplyTo}'"));
                
                return;
            }

            subscription.Write(message);
            link.AcceptMessage(message, true);
        }
        else if (subscriptionHandler.GetMessageSubscription().Any())
        {
            var subscriptions = subscriptionHandler.GetMessageSubscription();
            foreach (var subscription in subscriptions)
            {
                subscription.Write(message);
            }

            link.AcceptMessage(message, true);
        }
        else
        {
            logger.LogWarning("There is no subscriber in the topic '{Link}'", link.Name);
            link.RejectMessage(message, new AmqpException(AmqpErrorCode.NotAllowed,
                   $"There is no subscriber in the topic to process this message"));
        }
    }
}
