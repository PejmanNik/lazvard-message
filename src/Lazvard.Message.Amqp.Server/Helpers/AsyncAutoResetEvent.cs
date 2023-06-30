namespace Lazvard.Message.Amqp.Server.Helpers;

public sealed class AsyncAutoResetEvent
{
    private readonly object syncLock = new();
    private TaskCompletionSource taskSource;

    public AsyncAutoResetEvent()
    {
        taskSource = new TaskCompletionSource();
    }

    public async Task WaitAsync(CancellationToken stopToken)
    {
        await taskSource.Task.WaitAsync(stopToken);

        lock (syncLock)
        {
            taskSource = new TaskCompletionSource();
        }
    }

    public void Set()
    {
        lock (syncLock)
        {
            taskSource.TrySetResult();
        }
    }
}
