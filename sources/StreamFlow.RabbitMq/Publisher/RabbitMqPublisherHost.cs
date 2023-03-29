using Microsoft.Extensions.Logging;

namespace StreamFlow.RabbitMq.Publisher;

internal interface IRabbitMqPublisherHost
{
    bool IsRunning { get; }
}

internal class RabbitMqPublisherHost : IRabbitMqPublisherHost
{
    private readonly CancellationTokenSource _cts = new();

    private readonly IRabbitMqPublicationQueue _channel;
    private readonly IRabbitMqService _rabbitMq;
    private readonly IRabbitMqMetrics _metrics;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisherHost(IRabbitMqPublicationQueue channel
        , IRabbitMqServiceFactory rabbitMqFactory
        , IRabbitMqMetrics metrics
        , ILogger<RabbitMqPublisher> logger
        , IRabbitMqPublisherHostLifetime lifetime)
    {
        _channel = channel;
        _rabbitMq = rabbitMqFactory.Create();
        _metrics = metrics;
        _logger = logger;

        Task.Factory.StartNew(
            MonitorQueueAsync,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );

        IsRunning = true;

        lifetime.OnApplicationStopping(_cts.Cancel);
    }

    public bool IsRunning { get; private set; }

    private async Task MonitorQueueAsync()
    {
        _logger.LogWarning("RabbitMq publisher host started");

        var cancellationToken = _cts.Token;

        Exception? lastException = null;
        try
        {
            await foreach (var publication in _channel.ReadAllAsync(cancellationToken))
            {
                if (publication == null)
                {
                    continue;
                }

                publication.MarkAsDequeued();

                using var duration = _metrics.PublicationConsumed(publication.Context.Exchange ?? string.Empty);
                await _rabbitMq.PublishAsync(publication, cancellationToken);
                duration?.Complete();
            }
        }
        catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
        {
            lastException = e;
        }
        catch (Exception e)
        {
            lastException = e;
            _logger.LogError(e, "RabbitMq publisher host failed");
        }
        finally
        {
            IsRunning = false;
            _channel.Complete();

            await DrainAsync();

            _logger.LogWarning(lastException, "RabbitMq publisher host exited");
        }
    }

    private async Task DrainAsync()
    {
        try
        {
            var drainSuccess = 0;
            var drainFailure = 0;
            Exception? lastException = null;

            await foreach (var publication in _channel.ReadAllAsync(CancellationToken.None))
            {
                try
                {
                    publication?.Cancel();
                    drainSuccess += 1;
                }
                catch (Exception e)
                {
                    drainFailure += 1;
                    lastException = e;
                }
            }

            if (drainFailure > 0)
            {
                _logger.LogError(lastException, "RabbitMq publisher host failed to drain {DrainSuccess} publications, {DrainFailure} failed", drainSuccess, drainFailure);
            }
            else
            {
                _logger.LogWarning("RabbitMq publisher host drained {DrainSuccess} publications", drainSuccess);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "RabbitMq publisher host failed to drain");
        }
    }
}
