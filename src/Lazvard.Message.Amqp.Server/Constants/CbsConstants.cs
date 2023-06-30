using Microsoft.Azure.Amqp.Encoding;

namespace Lazvard.Message.Amqp.Server.Constants;

internal static class CbsConstants
{
    public static class PutToken
    {
        //
        // Summary:
        //     The put-token operation property value
        public const string OperationValue = "put-token";

        //
        // Summary:
        //     The token type property name
        public const string Type = "type";

        //
        // Summary:
        //     The audience property name
        public const string Audience = "name";

        //
        // Summary:
        //     The expiration property name
        internal const string Expiration = "expiration";

        //
        // Summary:
        //     The response status code property name
        public const string StatusCode = "status-code";

        //
        // Summary:
        //     The response status description property name
        public const string StatusDescription = "status-description";
    }

    //
    // Summary:
    //     The Property name for setting timeouts
    public static readonly AmqpSymbol TimeoutName = "com.microsoft:timeout";

    //
    // Summary:
    //     The address of the CBS Node ($cbs)
    public const string CbsAddress = "$cbs";

    //
    // Summary:
    //     The operation property name
    public const string Operation = "operation";
}
