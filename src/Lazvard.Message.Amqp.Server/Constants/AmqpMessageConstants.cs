namespace Lazvard.Message.Amqp.Server.Constants;

internal class AmqpMessageConstants
{
    public const string LockedUntil = "x-opt-locked-until";
    public const string LockToken = "x-opt-lock-token";
    public const string SequenceNumber = "x-opt-sequence-number";
    public const string EnqueueSequenceNumber = "x-opt-enqueue-sequence-number";
    public const string EnqueuedTime = "x-opt-enqueued-time";
    public const string MessageState = "x-opt-message-state";
}
