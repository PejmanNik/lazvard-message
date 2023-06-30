using Lazvard.Message.Amqp.Server.Constants;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Encoding;
using Microsoft.Azure.Amqp.Framing;

namespace Lazvard.Message.Amqp.Server.Helpers;

public class ResponseMessageBuilder
{
    private readonly AmqpMessage message;
    private readonly AmqpMap map;

    private ResponseMessageBuilder()
    {
        map = new AmqpMap();
        message = AmqpMessage.Create(new AmqpValue() { Value = map });
        message.ApplicationProperties = new ApplicationProperties();
    }

    public static ResponseMessageBuilder Success()
    {
        var builder = new ResponseMessageBuilder();
        builder.message.ApplicationProperties.Map.Add(ManagementConstants.Response.StatusCode, (int)AmqpResponseStatusCode.OK);

        return builder;
    }

    public static ResponseMessageBuilder Failed(AmqpResponseStatusCode statusCode)
    {
        var builder = new ResponseMessageBuilder();
        builder.message.ApplicationProperties.Map.Add(ManagementConstants.Response.StatusCode, (int)statusCode);

        return builder;
    }

    public ResponseMessageBuilder WithError(AmqpSymbol errorCondition, string statusDescription)
    {
        message.ApplicationProperties.Map.Add(ManagementConstants.Response.ErrorCondition, errorCondition);
        message.ApplicationProperties.Map.Add(ManagementConstants.Response.StatusDescription, statusDescription);
        return this;
    }

    public ResponseMessageBuilder ReplyTo(AmqpMessage request)
    {
        message.Properties.To = request.Properties.ReplyTo;
        message.Properties.CorrelationId = request.Properties.MessageId;

        return this;
    }

    public ResponseMessageBuilder WithProperty(MapKey key, object value)
    {
        map.Add(key, value);
        return this;
    }

    public ResponseMessageBuilder WithMessages(IEnumerable<AmqpMessage> messages)
    {
        map.Add(ManagementConstants.Properties.Messages,
            messages.Select(BuildMessageDataMap)
            .ToList() // the service bus client expect a list (not an array or ...)
        );

        return this;
    }

    private static AmqpMap BuildMessageDataMap(AmqpMessage message)
    {
        var map = new AmqpMap
        {
            { ManagementConstants.Properties.Message, message.GetMergedPayload() },
        };

        if (message.DeliveryTag.Array is not null && message.DeliveryTag.Array.Length == 16)
        {
            map.Add(ManagementConstants.Properties.LockToken, new Guid(message.DeliveryTag.Array));
        }

        return map;
    }

    public AmqpMessage Build()
    {
        return message;
    }
}
