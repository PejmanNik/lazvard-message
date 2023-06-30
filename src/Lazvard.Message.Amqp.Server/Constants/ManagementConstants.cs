using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Encoding;

namespace Lazvard.Message.Amqp.Server.Constants;

internal class ManagementConstants
{
    public const string Microsoft = "com.microsoft";

    public static class Request
    {
        public const string Operation = "operation";
        public const string AssociatedLinkName = "associated-link-name";
    }

    public static class Response
    {
        public const string StatusCode = "statusCode";
        public const string StatusDescription = "statusDescription";
        public const string ErrorCondition = "errorCondition";
    }

    public static class Operations
    {
        public const string RenewLockOperation = AmqpConstants.Vendor + ":renew-lock";
        public const string ReceiveBySequenceNumberOperation = AmqpConstants.Vendor + ":receive-by-sequence-number";
        public const string UpdateDispositionOperation = AmqpConstants.Vendor + ":update-disposition";
        public const string RenewSessionLockOperation = AmqpConstants.Vendor + ":renew-session-lock";
        public const string SetSessionStateOperation = AmqpConstants.Vendor + ":set-session-state";
        public const string GetSessionStateOperation = AmqpConstants.Vendor + ":get-session-state";
        public const string PeekMessageOperation = AmqpConstants.Vendor + ":peek-message";
        public const string AddRuleOperation = AmqpConstants.Vendor + ":add-rule";
        public const string RemoveRuleOperation = AmqpConstants.Vendor + ":remove-rule";
        public const string EnumerateRulesOperation = AmqpConstants.Vendor + ":enumerate-rules";
        public const string ScheduleMessageOperation = AmqpConstants.Vendor + ":schedule-message";
        public const string CancelScheduledMessageOperation = AmqpConstants.Vendor + ":cancel-scheduled-message";
    }

    public static class Properties
    {
        public static readonly MapKey ServerTimeout = new(AmqpConstants.Vendor + ":server-timeout");
        public static readonly MapKey TrackingId = new(AmqpConstants.Vendor + ":tracking-id");

        public static readonly MapKey SessionState = new("session-state");
        public static readonly MapKey LockToken = new("lock-token");
        public static readonly MapKey LockTokens = new("lock-tokens");
        public static readonly MapKey SequenceNumbers = new("sequence-numbers");
        public static readonly MapKey Expirations = new("expirations");
        public static readonly MapKey Expiration = new("expiration");
        public static readonly MapKey SessionId = new("session-id");
        public static readonly MapKey MessageId = new("message-id");
        public static readonly MapKey PartitionKey = new("partition-key");
        public static readonly MapKey ViaPartitionKey = new("via-partition-key");

        public static readonly MapKey ReceiverSettleMode = new("receiver-settle-mode");
        public static readonly MapKey Message = new("message");
        public static readonly MapKey Messages = new("messages");
        public static readonly MapKey DispositionStatus = new("disposition-status");
        public static readonly MapKey PropertiesToModify = new("properties-to-modify");
        public static readonly MapKey DeadLetterReason = new("deadletter-reason");
        public static readonly MapKey DeadLetterDescription = new("deadletter-description");

        public static readonly MapKey FromSequenceNumber = new("from-sequence-number");
        public static readonly MapKey MessageCount = new("message-count");

        public static readonly MapKey Skip = new("skip");
        public static readonly MapKey Top = new("top");
        public static readonly MapKey Rules = new("rules");
        public static readonly MapKey RuleName = new("rule-name");
        public static readonly MapKey RuleDescription = new("rule-description");
        public static readonly MapKey RuleCreatedAt = new("rule-created-at");
        public static readonly MapKey SqlRuleFilter = new("sql-filter");
        public static readonly MapKey SqlRuleAction = new("sql-rule-action");
        public static readonly MapKey CorrelationRuleFilter = new("correlation-filter");
        public static readonly MapKey Expression = new("expression");
        public static readonly MapKey CorrelationId = new("correlation-id");
        public static readonly MapKey To = new("to");
        public static readonly MapKey ReplyTo = new("reply-to");
        public static readonly MapKey Label = new("label");
        public static readonly MapKey ReplyToSessionId = new("reply-to-session-id");
        public static readonly MapKey ContentType = new("content-type");
        public static readonly MapKey CorrelationRuleFilterProperties = new("properties");
    }

    public static class Errors
    {
        public static readonly AmqpSymbol DeadLetterName = AmqpConstants.Vendor + ":dead-letter";
        public static readonly AmqpSymbol TimeoutError = AmqpConstants.Vendor + ":timeout";
        public static readonly AmqpSymbol AddressAlreadyInUseError = AmqpConstants.Vendor + ":address-already-in-use";
        public static readonly AmqpSymbol AuthorizationFailedError = AmqpConstants.Vendor + ":auth-failed";
        public static readonly AmqpSymbol MessageLockLostError = AmqpConstants.Vendor + ":message-lock-lost";
        public static readonly AmqpSymbol SessionLockLostError = AmqpConstants.Vendor + ":session-lock-lost";
        public static readonly AmqpSymbol StoreLockLostError = AmqpConstants.Vendor + ":store-lock-lost";
        public static readonly AmqpSymbol SessionCannotBeLockedError = AmqpConstants.Vendor + ":session-cannot-be-locked";
        public static readonly AmqpSymbol NoMatchingSubscriptionError = AmqpConstants.Vendor + ":no-matching-subscription";
        public static readonly AmqpSymbol ServerBusyError = AmqpConstants.Vendor + ":server-busy";
        public static readonly AmqpSymbol ArgumentError = AmqpConstants.Vendor + ":argument-error";
        public static readonly AmqpSymbol ArgumentOutOfRangeError = AmqpConstants.Vendor + ":argument-out-of-range";
        public static readonly AmqpSymbol PartitionNotOwnedError = AmqpConstants.Vendor + ":partition-not-owned";
        public static readonly AmqpSymbol EntityDisabledError = AmqpConstants.Vendor + ":entity-disabled";
        public static readonly AmqpSymbol PublisherRevokedError = AmqpConstants.Vendor + ":publisher-revoked";
        public static readonly AmqpSymbol OperationCancelledError = AmqpConstants.Vendor + ":operation-cancelled";
        public static readonly AmqpSymbol EntityAlreadyExistsError = AmqpConstants.Vendor + ":entity-already-exists";
        public static readonly AmqpSymbol RelayNotFoundError = AmqpConstants.Vendor + ":relay-not-found";
        public static readonly AmqpSymbol MessageNotFoundError = AmqpConstants.Vendor + ":message-not-found";
        public static readonly AmqpSymbol LockedUntilUtc = AmqpConstants.Vendor + ":locked-until-utc";
    }

    public static class DispositionStatus
    {
        public const string Completed = "completed";
        public const string Defered = "defered";
        public const string Suspended = "suspended";
        public const string Abandoned = "abandoned";
    }

}
