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
    private readonly ManualResetEventSlim _publisherCompleted = new ();

    public RabbitMqPublisherHostedService(IServiceProvider services, IRabbitMqPublisherChannel channel, IRabbitMqMetrics metrics, ILogger<RabbitMqPublisherHostedService> logger)
    {
        _services = services;
        _channel = channel;
        _metrics = metrics;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Starting RabbitMqPublisherHostedService");

        // ReSharper disable once MethodSupportsCancellation
        Task.Factory.StartNew(() => StartPublisherThread(cancellationToken));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Stopping RabbitMqPublisherHostedService");

        _channel.Complete();

        // wait as much as possible.. we do not want to lose messages
        _publisherCompleted.Wait(CancellationToken.None);

        return Task.CompletedTask;
    }

    private async Task StartPublisherThread(CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        try
        {
            using var scope = _services.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

            // do not use cancellation token here - if service is stopping
            // we still want to finish queue
            await foreach (var item in _channel.ReadAllAsync(CancellationToken.None))
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
                        var options = item.Options ?? new PublishOptions();
                        options.BusPublisher = true;
                        options.MetricsPrefix = "bus:";

                        await publisher
                            .PublishAsync(item.Message, options)
                            .ConfigureAwait(false);

                        published = true;

                        _metrics.PublishingByBus();
                    }
                    catch (Exception e)
                    {
                        _metrics.PublishingByBusError();

                        if (failedRetries % divider == 0)
                        {
                            _logger.LogWarning(e, "Unable to publish message. Failed retries: {FailedRetries}", failedRetries);
                        }

                        failedRetries += 1;

                        if (failedRetries % 600 == 0)
                        {
                            divider += 30;
                        }

                        // if we are in error mode and cancellation is requested - not much we can do here..
                        // just return from method
                        if (cancellationToken.IsCancellationRequested) return;

                        await Sleep(cancellationToken).ConfigureAwait(false);
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
            _publisherCompleted.Set();

            // mark bus as completed to avoid message accumulation
            _channel.Complete();

            _logger.LogWarning(lastException, "RabbitMqPublisherHostedService stopped.");
        }
    }

    private static async Task Sleep(CancellationToken cancellationToken)
    {
        try
        {
            // use cancellation token here to get out of sleep as soon as possible
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            //
        }
    }
}
