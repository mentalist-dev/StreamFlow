using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamFlow.RabbitMq.Publisher;

namespace StreamFlow.RabbitMq.Hosting;

internal class RabbitMqPublisherHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IRabbitMqPublisherChannel _channel;
    private readonly IRabbitMqMetrics _metrics;
    private readonly ILogger<RabbitMqPublisherHostedService> _logger;

    public RabbitMqPublisherHostedService(IServiceProvider services, IRabbitMqPublisherChannel channel, IRabbitMqMetrics metrics, ILogger<RabbitMqPublisherHostedService> logger)
    {
        _services = services;
        _channel = channel;
        _metrics = metrics;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // ReSharper disable once MethodSupportsCancellation
        Task.Factory.StartNew(() => StartPublisherThread(cancellationToken));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Complete();
        return Task.CompletedTask;
    }

    private async Task StartPublisherThread(CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        try
        {
            using var scope = _services.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

            var channel = _channel.ReadAllAsync(cancellationToken);
            await foreach (var item in channel.WithCancellation(cancellationToken))
            {
                if (item == null)
                    continue;

                var published = false;
                var failedRetries = 0;
                var divider = 30;
                while (!published)
                {
                    try
                    {
                        await publisher
                            .PublishAsync(item.Message, item.Options, cancellationToken)
                            .ConfigureAwait(false);

                        published = true;

                        _metrics.BusPublishing();
                    }
                    catch (Exception e)
                    {
                        _metrics.BusPublishingError();

                        if (failedRetries % divider == 0)
                        {
                            _logger.LogWarning(e, "Unable to publish message. Failed retries: {FailedRetries}", failedRetries);
                        }

                        failedRetries += 1;

                        if (failedRetries % 600 == 0)
                        {
                            divider += 30;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // service is stopping
        }
        catch (Exception e)
        {
            lastException = e;
        }
        finally
        {
            // mark bus as completed to avoid message accumulation
            _channel.Complete();

            _logger.LogWarning(lastException, "RabbitMqPublisherHostedService stopped.");
        }
    }
}

