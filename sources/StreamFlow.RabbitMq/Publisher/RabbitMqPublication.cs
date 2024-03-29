namespace StreamFlow.RabbitMq.Publisher;

internal sealed class RabbitMqPublication
{
    private readonly IDurationMetric? _duration;
    private readonly IDisposable? _queued;
    private readonly CancellationTokenSource _cts;
    private Exception? _exception;
    private bool _finished;

    public RabbitMqPublisherMessageContext Context { get; }
    public bool FireAndForget { get; }
    public TaskCompletionSource Completion { get; }
    public CancellationToken CancellationToken { get; }

    public RabbitMqPublication(IDurationMetric? duration, RabbitMqPublisherMessageContext context, CancellationToken cancellationToken, TimeSpan? timeout, IDisposable? queued, bool fireAndForget = false)
    {
        _duration = duration;
        _queued = queued;

        Context = context;
        FireAndForget = fireAndForget;
        Completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _cts = cancellationToken != default
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(60));

        CancellationToken = _cts.Token;

        CancellationToken.Register(() => CancelInternal(false));
    }

    public void Complete()
    {
        CompleteInternal();
    }

    public void Fail(Exception exception)
    {
        FailInternal(exception);
    }

    public void Cancel()
    {
        CancelInternal(true);
    }

    public void MarkStateAsFailed(Exception e)
    {
        _exception = e is not OperationCanceledException ? e : null;
    }

    public void MarkAsDequeued()
    {
        try
        {
            _queued?.Dispose();
        }
        catch
        {
            //
        }
    }

    private void CompleteInternal()
    {
        try
        {
            _exception = null;
            Completion.TrySetResult();
            _duration?.Complete();
        }
        finally
        {
            Cleanup();
        }
    }

    private void FailInternal(Exception exception)
    {
        try
        {
            Completion.TrySetException(exception);
        }
        finally
        {
            Cleanup();
        }
    }

    private void CancelInternal(bool ignoreExceptions)
    {
        try
        {
            var exception = _exception;
            if (ignoreExceptions || exception == null)
            {
                Completion.TrySetCanceled(CancellationToken);
                _duration?.Complete("cancelled");
            }
            else
            {
                Completion.TrySetException(exception);
            }
        }
        finally
        {
            Cleanup();
        }
    }

    private void Cleanup()
    {
        if (!_finished)
        {
            try
            {
                _duration?.Dispose();
            }
            catch
            {
                //
            }

            try
            {
                _cts.Dispose();
            }
            catch
            {
                //
            }
            finally
            {
                _finished = true;
            }
        }
    }
}
