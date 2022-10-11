using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace StreamFlow.RabbitMq.Publisher;

internal class RabbitMqPublisherHost : IHostedService
{
    private readonly CancellationTokenSource _cts = new();

    private readonly IRabbitMqPublicationQueue _channel;
    private readonly IRabbitMqService _rabbitMq;
    private readonly IRabbitMqMetrics _metrics;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisherHost(IRabbitMqPublicationQueue channel
        , IRabbitMqServiceFactory rabbitMqFactory
        , IRabbitMqMetrics metrics
        , ILogger<RabbitMqPublisher> logger)
    {
        _channel = channel;
        _rabbitMq = rabbitMqFactory.Create();
        _metrics = metrics;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Factory.StartNew(
            MonitorQueueAsync,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    private async Task MonitorQueueAsync()
    {
        _logger.LogWarning("RabbitMq publisher host started");

        try
        {
            var cancellationToken = _cts.Token;
            await foreach (var publication in _channel.ReadAllAsync(cancellationToken))
            {
                if (publication == null)
                    continue;

                using var duration = _metrics.PublicationConsumed(publication.Context.Exchange ?? string.Empty);
                await _rabbitMq.PublishAsync(publication, cancellationToken);
                duration?.Complete();
            }
        }
        catch (Exception e)
        {
            _channel.Complete();
            _logger.LogError(e, "RabbitMq publisher host failed");
        }
        finally
        {
            _logger.LogWarning("RabbitMq publisher host exited");
        }
    }
}
