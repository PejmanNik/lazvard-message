namespace Lazvard.Message.Amqp.Server;

abstract class AsyncResult : IAsyncResult
{
    private readonly AsyncCallback callback;
    private readonly object lockObject;

    protected AsyncResult(AsyncCallback callback, object state)
    {
        this.callback = callback;
        lockObject = new object();
        AsyncState = state;
    }

    public object? AsyncState { private set; get; }

    public WaitHandle AsyncWaitHandle => throw new NotImplementedException();

    public bool CompletedSynchronously { private set; get; }

    public bool IsCompleted { private set; get; }

    protected bool Complete(bool didCompleteSynchronously)
    {
        lock (lockObject)
        {
            if (IsCompleted)
            {
                return false;
            }

            IsCompleted = true;
        }

        CompletedSynchronously = didCompleteSynchronously;
        callback?.Invoke(this);

        return true;
    }
}


class CompletedAsyncResult : AsyncResult
{
    public CompletedAsyncResult(AsyncCallback callback, object state)
        : base(callback, state)
    {
        Complete(true);
    }

}
