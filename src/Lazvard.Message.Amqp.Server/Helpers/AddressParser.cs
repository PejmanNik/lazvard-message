using Microsoft.Azure.Amqp.Framing;

namespace Lazvard.Message.Amqp.Server.Helpers;

static class AddressParser
{
    public class AddressInfo
    {
        public AddressInfo(string node, string? subscription)
        {
            Node = node;
            Subscription = subscription;
        }

        public string Node { get; }
        public string? Subscription { get; }
    }

    public static AddressInfo Parse(Address address)
    {
        var strValue = address.ToString() ?? "";
        var parts = strValue.Split("/", StringSplitOptions.RemoveEmptyEntries);

        // {topicName}/Subscriptions/{subscriptionName}
        return new AddressInfo(parts[0], string.Join("/", parts.Skip(2)));
    }
}
