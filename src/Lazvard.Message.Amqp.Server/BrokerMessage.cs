using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.Azure.Amqp;

namespace Lazvard.Message.Amqp.Server;

public record class BrokerMessage
{
    public AmqpMessage Message { get; }
    public bool IsDeferred { get; init; }
    public bool IsLocked { get; init; }
    public DateTime LockedUntil { get; init; }
    public Guid LockToken { get; init; }
    public string LockHolderLink { get; init; }

    public string TraceId => Message.GetTraceId();

    public BrokerMessage(AmqpMessage message)
    {
        Message = message;
        LockHolderLink = "";
    }

    public BrokerMessage Defer()
    {
        AssertStatus(x => x.IsLocked);

        return this with { IsDeferred = true, IsLocked = false };
    }

    public BrokerMessage Lock(Guid lockToken, DateTime lockedUntil, string lockHolderLink)
    {
        AssertStatus(x => !x.IsLocked);
        return this with { IsLocked = true, LockedUntil = lockedUntil, LockToken = lockToken, LockHolderLink = lockHolderLink };
    }

    public BrokerMessage RenewLock(DateTime lockedUntil)
    {
        AssertStatus(x => x.IsLocked);
        return this with { LockedUntil = lockedUntil };
    }

    public BrokerMessage Unlock()
    {
        return this with { IsLocked = false, LockedUntil = default, LockToken = default, LockHolderLink = "" };
    }

    public void AssertStatus(Func<BrokerMessage, bool> assert)
    {
        if (!assert(this))
        {
            throw new InvalidOperationException($"The message {TraceId} status is invalid.");
        }
    }
}
