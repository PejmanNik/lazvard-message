using Microsoft.Azure.Amqp;
using Microsoft.Extensions.Logging;

namespace Lazvard.Message.Amqp.Server;

public class ConsumerFactory
{
    private readonly ILoggerFactory loggerFactory;

    public ConsumerFactory(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    public Consumer Create(SendingAmqpLink link, IMessageQueue messageQueue, Action runDeliverMessages)
    {
        return new Consumer(link, messageQueue, runDeliverMessages, loggerFactory);
    }
}
