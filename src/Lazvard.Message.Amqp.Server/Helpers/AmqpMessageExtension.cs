using Lazvard.Message.Amqp.Server.Constants;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Encoding;
using Microsoft.Azure.Amqp.Framing;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Lazvard.Message.Amqp.Server.Helpers;

static class AmqpMessageExtension
{
    public static long GetSequenceNumber(this AmqpMessage message)
    {
        message.MessageAnnotations.Map.TryGetValue(
            AmqpMessageConstants.SequenceNumber,
            out object val);

        return (long?)val ?? 0;
    }

    public static string GetTraceId(this AmqpMessage message)
    {
        return $"{GetSequenceNumber(message)}-{message.GetHashCode()}";
    }

    private static Lazy<FieldInfo> GetPayloadInitializedField(AmqpMessage message) => new(() => message
        .GetType()
        .GetField("payloadInitialized", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new ArgumentException("can't find payloadInitialized"));

    private static Lazy<Dictionary<string, PropertyInfo>> GetProperties(AmqpMessage message) => new(() => message
        .GetType()
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .ToDictionary(x => x.Name));

    public static AmqpMessage Clone(this AmqpMessage message, bool deepClone = false)
    {
        // Microsoft.Azure.Amqp v2 don't have a suitable clone method
        var cloned = message.Clone();
        GetPayloadInitializedField(cloned).Value.SetValue(cloned, false);
        var properties = GetProperties(cloned).Value;

        if (!deepClone)
        {
            cloned.MessageAnnotations = message.MessageAnnotations;
            cloned.ApplicationProperties = message.ApplicationProperties;

            properties[nameof(AmqpMessage.Header)].SetValue(cloned, message.Header);
            properties[nameof(AmqpMessage.DeliveryAnnotations)].SetValue(cloned, message.DeliveryAnnotations);
            properties[nameof(AmqpMessage.Properties)].SetValue(cloned, message.Properties);
            properties[nameof(AmqpMessage.Footer)].SetValue(cloned, message.Footer);
        }
        else
        {
            if (message.Header != null)
            {
                properties[nameof(AmqpMessage.Header)].SetValue(cloned, new Header());
                cloned.Header.Durable = message.Header.Durable;
                cloned.Header.Priority = message.Header.Priority;
                cloned.Header.Ttl = message.Header.Ttl;
                cloned.Header.FirstAcquirer = message.Header.FirstAcquirer;
                cloned.Header.DeliveryCount = message.Header.DeliveryCount;
            }
            else
            {
                properties[nameof(AmqpMessage.Header)].SetValue(cloned, new Header());
            }

            if (message.DeliveryAnnotations != null)
            {
                properties[nameof(AmqpMessage.DeliveryAnnotations)].SetValue(cloned, new DeliveryAnnotations());
                cloned.DeliveryAnnotations.Map.Merge(message.DeliveryAnnotations.Map);
            }
            else
            {
                properties[nameof(AmqpMessage.DeliveryAnnotations)].SetValue(cloned, new DeliveryAnnotations());
            }

            if (message.MessageAnnotations != null)
            {
                properties[nameof(AmqpMessage.MessageAnnotations)].SetValue(cloned, new MessageAnnotations());
                cloned.MessageAnnotations.Map.Merge(message.MessageAnnotations.Map);
            }
            else
            {
                properties[nameof(AmqpMessage.MessageAnnotations)].SetValue(cloned, new MessageAnnotations());
            }

            if (message.ApplicationProperties != null)
            {
                properties[nameof(AmqpMessage.ApplicationProperties)].SetValue(cloned, new ApplicationProperties());
                cloned.ApplicationProperties.Map.Merge(message.ApplicationProperties.Map);
            }

            if (message.Properties != null)
            {
                properties[nameof(AmqpMessage.Properties)].SetValue(cloned, new Properties());

                cloned.Properties.MessageId = message.Properties.MessageId;
                cloned.Properties.UserId = message.Properties.UserId;
                cloned.Properties.To = message.Properties.To;
                cloned.Properties.Subject = message.Properties.Subject;
                cloned.Properties.ReplyTo = message.Properties.ReplyTo;
                cloned.Properties.CorrelationId = message.Properties.CorrelationId;
                cloned.Properties.ContentType = message.Properties.ContentType;
                cloned.Properties.ContentEncoding = message.Properties.ContentEncoding;
                cloned.Properties.AbsoluteExpiryTime = message.Properties.AbsoluteExpiryTime;
                cloned.Properties.CreationTime = message.Properties.CreationTime;
                cloned.Properties.GroupId = message.Properties.GroupId;
                cloned.Properties.GroupSequence = message.Properties.GroupSequence;
                cloned.Properties.ReplyToGroupId = message.Properties.ReplyToGroupId;
            }
            else
            {
                properties[nameof(AmqpMessage.Properties)].SetValue(cloned, new Properties());
            }

            if (message.Footer != null)
            {
                properties[nameof(AmqpMessage.Footer)].SetValue(cloned, new Footer());
                cloned.Footer.Map.Merge(message.Footer.Map);
            }
        }


        return cloned;
    }

    public static ArraySegment<byte> GetMergedPayload(this AmqpMessage message)
    {
        var data = message.GetPayload().SelectMany(x => x).ToArray();
        return new ArraySegment<byte>(data);
    }

    public static Result<T> ReadAsMap<T>(this AmqpMessage message, MapKey key)
    {
        if (message.ValueBody.Value is not AmqpMap map)
        {
            return Result.Fail();
        }

        if (!map.TryGetValue(key, out T value))
        {
            return Result.Fail();
        }

        return value;
    }

    public static bool GetAppProperties(this AmqpMessage message, string key, [NotNullWhen(true)] out string? value)
    {
        value = null;
        return message.ApplicationProperties.Map?.TryGetValue(key, out value) == true;
    }
}
