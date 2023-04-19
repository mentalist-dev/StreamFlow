using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using StreamFlow.Server;

namespace StreamFlow.RabbitMq.Server;

internal sealed class RabbitMqConsumerController: IDisposable
{
    private readonly Channel<DateTime> _recoveryChannel = Channel.CreateBounded<DateTime>(1);

    private readonly IServiceProvider _services;
    private readonly IConsumerRegistration _registration;
    private readonly RabbitMqConsumerInfo _consumerInfo;
    private readonly StreamFlowDefaults _defaults;
    private readonly IConnection _connection;
    private readonly IRabbitMqMetrics _metrics;
    private readonly ILogger<IRabbitMqConsumer> _logger;
    private readonly List<KeyValuePair<string, object>> _loggerState;
    private readonly CancellationToken _cancellationToken;

    private CancellationTokenSource? _cancellationTokenSource;
    private RabbitMqConsumer? _consumer;
    private bool _initialized;
    private bool _disposing;
    private bool _disposed;

    public RabbitMqConsumerController(IServiceProvider services
        , IConsumerRegistration registration
        , RabbitMqConsumerInfo consumerInfo
        , StreamFlowDefaults defaults
        , IConnection connection
        , IRabbitMqMetrics metrics
        , ILogger<IRabbitMqConsumer> logger
        , CancellationToken cancellationToken)
    {
        _services = services;
        _registration = registration;
        _consumerInfo = consumerInfo;
        _defaults = defaults;
        _connection = connection;
        _metrics = metrics;
        _logger = logger;
        _cancellationToken = cancellationToken;

        _loggerState = new List<KeyValuePair<string, object>> { new("ConsumerId", _consumerInfo.Id) };

        Task.Factory.StartNew(
            RecoveryMonitor,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    public void Start()
    {
        if (_disposing || _disposed)
            throw new ObjectDisposedException("Controller already disposed.");

        if (!_initialized)
        {
            using var scope = _logger.BeginScope(_loggerState);
            CreateAndStartConsumer(_consumerInfo);
            _initialized = true;
        }
    }

    public void Dispose()
    {
        _disposing = true;

        var consumer = _consumer;
        if (consumer != null)
        {
            using var scope = _logger.BeginScope(_loggerState);

            DestroyCurrentConsumer(consumer);
            _consumer = null;
        }

        _recoveryChannel.Writer.TryComplete();

        _disposed = true;

        GC.SuppressFinalize(this);
    }

    ~RabbitMqConsumerController()
    {
        _disposed = true;
    }

    private async Task RecoveryMonitor()
    {
        try
        {
            var consumerInfo = _consumerInfo;

            await foreach (var _ in _recoveryChannel.Reader.ReadAllAsync(_cancellationToken))
            {
                using var scope = _logger.BeginScope(_loggerState);

                try
                {
                    _logger.LogWarning("Channel crashed, will start recovery.");

                    await RecoverConsumerInternal(consumerInfo);

                    _logger.LogWarning("Recovery finished");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Recovery failed");
                }
            }
        }
        catch (OperationCanceledException e)
        {
            _logger.LogInformation(e, "Recovery monitor canceled!");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Recovery monitor crashed!");
        }
    }

    private async Task RecoverConsumerInternal(RabbitMqConsumerInfo consumerInfo)
    {
        const int maxAllowedRetries = 10;
        var allowedRetries = maxAllowedRetries;
        while (allowedRetries > 0)
        {
            try
            {
                allowedRetries -= 1;

                var consumer = _consumer;
                if (consumer != null)
                {
                    try
                    {
                        DestroyCurrentConsumer(consumer);
                        _consumer = null;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Unable to destroy current consumer ");
                    }
                }

                CreateAndStartConsumer(consumerInfo);

                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Recovery failed, retries left {Retries}", allowedRetries);

                if (allowedRetries > 0)
                {
                    var delay = maxAllowedRetries - allowedRetries + 1;
                    await Task.Delay(TimeSpan.FromSeconds(10 * delay), _cancellationToken);
                }
            }
        }
    }

    private void CreateAndStartConsumer(RabbitMqConsumerInfo consumerInfo)
    {
        if (_disposing || _disposed)
            return;

        _consumer = new RabbitMqConsumer(_services, _connection, consumerInfo, _defaults, _metrics, _logger, _loggerState);
        _consumer.ChannelCrashed += (_, _) =>
        {
            _recoveryChannel.Writer.TryWrite(DateTime.UtcNow);
        };

        _logger.LogInformation("Created new consumer. Consumer info: {@ConsumerInfo}.", consumerInfo);

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

        _consumer.Start(_registration, _cancellationTokenSource.Token);
    }

    #region Destroy Consumer

    private void DestroyCurrentConsumer(RabbitMqConsumer consumer)
    {
        lock (this)
        {
            var canceledConsumerTag = CancelConsumerAtServer(consumer);

            if (!string.IsNullOrWhiteSpace(canceledConsumerTag))
            {
                _logger.LogWarning(
                    "Destroying consumer instance {ConsumerTag} for Queue = {QueueName} using Routing Key = {RoutingKey}.",
                    canceledConsumerTag, _consumerInfo.Queue, _consumerInfo.RoutingKey);
            }

            CancelCurrentExecutions();
            DisposeConsumer(consumer);
        }
    }

    private string? CancelConsumerAtServer(RabbitMqConsumer consumer)
    {
        try
        {
            _logger.LogDebug(
                "Cancelling consumer {ConsumerTag} subscription at server.",
                consumer.ConsumerTag
            );

            return consumer.Cancel();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to cancel consumer.");
        }

        return null;
    }

    private void CancelCurrentExecutions()
    {
        try
        {
            _logger.LogDebug("Consumer marking executions as cancelled");
            _cancellationTokenSource?.Cancel();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to cancel consumer");
        }

        try
        {
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to dispose cancellation token source");
        }
        finally
        {
            _cancellationTokenSource = null;
        }
    }

    private void DisposeConsumer(RabbitMqConsumer consumer)
    {
        try
        {
            _logger.LogTrace("Disposing consumer");
            consumer.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Consumer dispose failed");
        }
    }

    #endregion
}
