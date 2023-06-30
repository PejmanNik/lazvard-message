using Lazvard.Message.Amqp.Server.Helpers;
using System.Collections.Concurrent;

namespace Lazvard.Message.Amqp.Server;

interface IExpirationList
{
    int Count { get; }

    bool TryAdd(BrokerMessage message);
    Result<BrokerMessage> TryGet(Guid lockToken, string holderLink);
    Result<BrokerMessage> TryRemove(Guid lockToken, string holderLink);
}

public class ExpirationList : IExpirationList
{
    private readonly TimeSpan expirationTime;
    private readonly CancellationToken stopToken;

    private readonly ConcurrentDictionary<string, BrokerMessage> items;
    private volatile TaskCompletionSource<bool> cleanupSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<BrokerMessage> onExpiration;

    public ExpirationList(
        TimeSpan expirationTime,
        Action<BrokerMessage> onExpiration,
        CancellationToken stopToken)
    {
        items = new();
        cleanupSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        this.expirationTime = expirationTime;
        this.onExpiration = onExpiration;
        this.stopToken = stopToken;

        _ = CheckExpirationAsync();
    }

    public int Count => items.Count;



    private async Task CheckExpirationAsync()
    {
        while (!stopToken.IsCancellationRequested)
        {
            try
            {
                await cleanupSource.Task;
                await Task.Delay(expirationTime, stopToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            foreach (var item in items.ToArray())
            {
                if (item.Value.LockedUntil <= DateTime.UtcNow)
                {
                    items.TryRemove(item.Key, out _);
                    onExpiration(item.Value);
                }
            }

            if (items.IsEmpty)
            {
                cleanupSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    public bool TryAdd(BrokerMessage message)
    {
        if (items.TryAdd($"{message.LockToken}#{message.LockHolderLink}", message))
        {
            cleanupSource.TrySetResult(true);
            return true;
        }

        return false;
    }

    public Result<BrokerMessage> TryGet(Guid lockToken, string holderLink)
    {
        if (items.TryGetValue(BuildKey(lockToken, holderLink), out var message))
        {
            return message;
        }

        return Result.Fail();
    }

    public Result<BrokerMessage> TryRemove(Guid lockToken, string holderLink)
    {
        if (items.TryRemove(BuildKey(lockToken, holderLink), out var message))
        {
            return message;
        }

        return Result.Fail();
    }

    private static string BuildKey(Guid lockToken, string holderLink)
        => $"{lockToken}#{holderLink}";
}
