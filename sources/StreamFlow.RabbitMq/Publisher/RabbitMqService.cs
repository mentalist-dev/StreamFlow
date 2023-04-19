using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace StreamFlow.RabbitMq.Publisher;

internal interface IRabbitMqService
{
    Task PublishAsync(RabbitMqPublication task, CancellationToken cancellationToken);
}

internal sealed class RabbitMqService: IRabbitMqService
{
    private static readonly Dictionary<string, DateTime> Exchanges = new();
    private static readonly object ExchangesLock = new();

    private readonly IRabbitMqPublisherConnection _connection;
    private readonly IRabbitMqMetrics _metrics;
    private readonly ILogger<RabbitMqPublisher> _logger;

    private RabbitMqPublisherChannel? _channel;

    public RabbitMqService(IRabbitMqPublisherConnection connection, IRabbitMqMetrics metrics, ILogger<RabbitMqPublisher> logger)
    {
        _connection = connection;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task PublishAsync(RabbitMqPublication task, CancellationToken cancellationToken)
    {
        var finished = false;
        var retries = 0;
        while (!finished)
        {
            try
            {
                if (task.CancellationToken.IsCancellationRequested)
                {
                    _metrics.PublisherError(task.Context.Exchange ?? string.Empty, PublicationExceptionReason.Cancelled);

                    task.Cancel();
                    finished = true;
                    continue;
                }

                if (_channel == null || _channel.Channel.IsClosed)
                {
                    _channel?.Dispose();
                    _channel = CreateChannel();
                }

                await PublishMessageAsync(_channel, task);

                finished = true;
            }
            catch (OperationCanceledException e)
            {
                _metrics.PublisherError(task.Context.Exchange ?? string.Empty, PublicationExceptionReason.Cancelled);

                finished = OnOperationCancelled(task, e);
            }
            catch (OperationInterruptedException e)
            {
                _metrics.PublisherError(task.Context.Exchange ?? string.Empty, PublicationExceptionReason.OperationInterrupted);

                // must recreate channel after this exception
                _channel?.Dispose();
                _channel = null;

                finished = OnOperationInterruptedException(task, e, retries);
            }
            catch (RabbitMqPublisherException e)
            {
                _metrics.PublisherError(task.Context.Exchange ?? string.Empty, e.Reason);

                finished = OnPublisherException(task, e, retries);
            }
            catch (Exception e)
            {
                _metrics.PublisherError(task.Context.Exchange ?? string.Empty, PublicationExceptionReason.Unknown);

                finished = OnException(task, e, retries);
            }

            if (!finished)
            {
                var delay = retries < 6 ? Math.Pow(2, retries) : 60;
                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                retries += 1;
            }
        }
    }

    private Task PublishMessageAsync(RabbitMqPublisherChannel channel, RabbitMqPublication publication)
    {
        if (publication.Context.DeclareExchange)
        {
            EnsureExchangeExists(publication.Context.Exchange);
        }

        return channel.PublishAsync(publication);
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
