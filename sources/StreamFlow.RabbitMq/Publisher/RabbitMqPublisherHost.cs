using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Exceptions;
using StreamFlow.Pipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace StreamFlow.RabbitMq.Publisher;

internal class RabbitMqPublisherHost : IHostedService
{
    private static readonly Dictionary<string, DateTime> Exchanges = new();
    private static readonly object ExchangesLock = new ();

    private readonly CancellationTokenSource _cts = new();

    private readonly IRabbitMqPublicationQueue _channel;
    private readonly IStreamFlowPublisherPipe _pipe;
    private readonly IRabbitMqPublisherConnection _connection;
    private readonly IServiceProvider _services;
    private readonly IRabbitMqMetrics _metrics;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisherHost(IRabbitMqPublicationQueue channel
        , IStreamFlowPublisherPipe pipe
        , IRabbitMqPublisherConnection connection
        , IServiceProvider services
        , IRabbitMqMetrics metrics
        , ILogger<RabbitMqPublisher> logger)
    {
        _channel = channel;
        _pipe = pipe;
        _connection = connection;
        _services = services;
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
            await MonitorQueueAsync(_cts.Token);
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

    private async Task MonitorQueueAsync(CancellationToken cancellationToken)
    {
        RabbitMqPublisherChannel? channel = null;
        await foreach (var publication in _channel.ReadAllAsync(cancellationToken))
        {
            if (publication == null)
                continue;

            using var duration = _metrics.PublicationConsumed(publication.Context.Exchange ?? string.Empty);
            channel = await PublishMessageAsync(publication, channel, cancellationToken);
            duration?.Complete();
        }
    }

    private async Task<RabbitMqPublisherChannel?> PublishMessageAsync(RabbitMqPublication task, RabbitMqPublisherChannel? channel, CancellationToken cancellationToken)
    {
        var finished = false;
        var retries = 0;
        while (!finished)
        {
            try
            {
                if (task.CancellationToken.IsCancellationRequested)
                {
                    task.Cancel();
                    finished = true;
                    continue;
                }

                if (channel == null || channel.Channel.IsClosed)
                {
                    channel?.Dispose();
                    channel = CreateChannel();
                }

                await PublishMessageAsync(channel, task);

                finished = true;
            }
            catch (OperationCanceledException e)
            {
                finished = OnOperationCancelled(task, e);
            }
            catch (OperationInterruptedException e)
            {
                // must recreate channel after this exception
                channel?.Dispose();
                channel = null;

                finished = OnOperationInterruptedException(task, e, retries);
            }
            catch (RabbitMqPublisherException e)
            {
                finished = OnPublisherException(task, e, retries);
            }
            catch (Exception e)
            {
                finished = OnException(task, e, retries);
            }

            if (!finished)
            {
                var delay = retries < 6 ? Math.Pow(2, retries) : 60;
                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                retries += 1;
            }
        }

        return channel;
    }

    private async Task PublishMessageAsync(RabbitMqPublisherChannel channel, RabbitMqPublication publication)
    {
        if (publication.Context.DeclareExchange)
        {
            EnsureExchangeExists(publication.Context.Exchange);
        }

        await using var scope = _services.CreateAsyncScope();
        await _pipe
            .ExecuteAsync(_services, publication.Context, ctx => channel.PublishAsync(ctx, publication))
            .ConfigureAwait(false);
    }

    private RabbitMqPublisherChannel CreateChannel()
    {
        _logger.LogDebug("Creating new publication channel");
        return new RabbitMqPublisherChannel(_connection.Get(), _logger);
    }

    private bool OnOperationCancelled(RabbitMqPublication task, OperationCanceledException e)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(e, "Message publication cancelled");
        }

        task.Cancel();
        return true;
    }

    private bool OnOperationInterruptedException(RabbitMqPublication task, OperationInterruptedException e, int retries)
    {
        if (e.ShutdownReason.ReplyCode == Constants.NotFound) // exchange not found!
        {
            task.Fail(e);
            return true;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(e, "RabbitMq connection issues. Try: {SequenceNo}", retries + 1);
        }

        // should retry
        task.MarkStateAsFailed(e);
        return false;
    }

    private bool OnPublisherException(RabbitMqPublication task, RabbitMqPublisherException e, int retries)
    {
        if (e.Reason != PublicationExceptionReason.Returned)
        {
            if (task.Context.IgnoreNoRoutes || task.FireAndForget)
            {
                task.Complete();
                return true;
            }

            _logger.LogError(e, "Unable to publish message. Try: {SequenceNo}", retries + 1);

            // should retry
            task.MarkStateAsFailed(e);
            return false;
        }

        if (e.Reason == PublicationExceptionReason.ExchangeNotFound && (task.Context.IgnoreNoRoutes || task.FireAndForget))
        {
            task.Complete();
            return true;
        }

        // message is not route-able
        task.Fail(e);
        return true;
    }

    private bool OnException(RabbitMqPublication task, Exception e, int retries)
    {
        _logger.LogError(e, "Unable to publish message. Try: {SequenceNo}", retries + 1);
        // should retry
        task.MarkStateAsFailed(e);
        return false;
    }

    private void EnsureExchangeExists(string? exchangeName)
    {
        if (string.IsNullOrWhiteSpace(exchangeName))
        {
            return;
        }

        if (!Exchanges.ContainsKey(exchangeName))
        {
            lock (ExchangesLock)
            {
                if (!Exchanges.ContainsKey(exchangeName))
                {
                    using var rabbitMq = new RabbitMqPublisherChannel(_connection.Get(), _logger);
                    rabbitMq.Channel.TryCreateExchange(_logger, exchangeName);
                    Exchanges[exchangeName] = DateTime.UtcNow;
                }
            }
        }
    }
}
