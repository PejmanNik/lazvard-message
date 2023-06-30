namespace Lazvard.Message.Amqp.Server.Helpers;

public sealed class AsyncManualResetEvent
{
    private readonly object syncLock = new();
    private TaskCompletionSource taskSource;

    public AsyncManualResetEvent()
    {
        taskSource = new TaskCompletionSource();
    }

    public AsyncManualResetEvent(bool initialState)
    {
        taskSource = new TaskCompletionSource();
        if (initialState)
        {
            taskSource.SetResult();
        }
    }

    public void Reset()
    {
        lock (syncLock)
        {
            if (taskSource.Task.IsCompleted)
                taskSource = new TaskCompletionSource();
        }
    }

    public Task WaitAsync(CancellationToken stopToken)
    {
        return taskSource.Task.WaitAsync(stopToken);
    }

    public void Set()
    {
        taskSource.TrySetResult();
    }
}
