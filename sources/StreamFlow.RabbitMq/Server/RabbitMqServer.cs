using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using StreamFlow.Configuration;
using StreamFlow.Server;

namespace StreamFlow.RabbitMq.Server;

public interface IRabbitMqServer
{
    RabbitMqServerState GetState();
    
    Task Start(IConsumerRegistration consumerRegistration, TimeSpan timeout, CancellationToken cancellationToken);
    void Stop();
}

public class RabbitMqServerState
{
    public bool IsConnected { get; }
    public int Registrations { get; }

    public RabbitMqServerState(bool isConnected, int registrations)
    {
        IsConnected = isConnected;
        Registrations = registrations;
    }
}

public class RabbitMqServer: IRabbitMqServer, IDisposable
{
    private static readonly TimeSpan ConnectionClosedTolerance = TimeSpan.FromMinutes(10);

    private readonly IServiceProvider _services;
    private readonly IRabbitMqConnection _connection;
    private readonly RabbitMqConsumerOptions _options;
    private readonly IRabbitMqConventions _conventions;
    private readonly StreamFlowOptions _defaults;
    private readonly IRabbitMqMetrics _metrics;
    private readonly List<IConsumerRegistration> _consumerRegistrations = new();
    private readonly List<RabbitMqConsumerController> _consumerControllers = new();
    private readonly ILogger<IRabbitMqConsumer> _logger;

    private IConnection? _physicalConnection;
    private DateTime? _physicalConnectionClosedSince = DateTime.UtcNow;

    public RabbitMqServer(IServiceProvider services
        , IRabbitMqConnection connection
        , RabbitMqConsumerOptions options
        , IRabbitMqConventions conventions
        , StreamFlowOptions defaults
        , IRabbitMqMetrics metrics
        , ILogger<IRabbitMqConsumer> logger)
    {
        _logger = logger;

        _services = services;
        _connection = connection;
        _options = options;
        _conventions = conventions;
        _defaults = defaults;
        _metrics = metrics;
    }

    public RabbitMqServerState GetState()
    {
        return new RabbitMqServerState(IsConnected(), _consumerRegistrations.Count);
    }

    public async Task Start(IConsumerRegistration consumerRegistration, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var requestType = consumerRegistration.RequestType;
        var consumerType = consumerRegistration.ConsumerType;
        var consumerGroup = consumerRegistration.Options.ConsumerGroup;
        var defaults = consumerRegistration.Default;

        _logger.LogInformation(
            "Starting consumer registration {ConsumerType} for message {RequestType} in group {ConsumerGroup}",
            consumerType, requestType, consumerGroup);

        var connection = await GetConnection(timeout, cancellationToken).ConfigureAwait(false);
        if (connection == null)
            return;

        var exchange = _conventions.GetExchangeName(requestType);
        var queue = _conventions.GetQueueName(requestType, consumerType, consumerGroup);
        var routingKey = "#";

        if (string.IsNullOrWhiteSpace(exchange))
            throw new Exception($"Unable to resolve exchange name from request type [{requestType}]");

        if (string.IsNullOrWhiteSpace(queue))
            throw new Exception($"Unable to resolve queue name from consumer type [{consumerType}] with consumer group [{consumerGroup}]");

        TryCreateTopology(connection, defaults, consumerRegistration.Options.Queue, exchange, queue, routingKey);

        var consumerCount = consumerRegistration.Options.ConsumerCount;
        if (consumerCount < 1)
        {
            consumerCount = 1;
        }

        var prefetchCount = consumerRegistration.Options.PrefetchCount ?? _options.PrefetchCount;
        var streamFlowDefaults = _defaults.Default ?? new StreamFlowDefaults();

        _consumerRegistrations.Add(consumerRegistration);

        for (var i = 0; i < consumerCount; i++)
        {
            var consumerInfo = new RabbitMqConsumerInfo(exchange, queue, routingKey, prefetchCount);

            var controller = new RabbitMqConsumerController(_services, consumerRegistration, consumerInfo, streamFlowDefaults, connection, _metrics, _logger, cancellationToken);
            controller.Start();

            _consumerControllers.Add(controller);
        }
    }

    public void Stop()
    {
        foreach (var controller in _consumerControllers)
        {
            controller.Dispose();
        }

        _consumerControllers.Clear();
    }

    public void Dispose()
    {
        foreach (var controller in _consumerControllers)
        {
            controller.Dispose();
        }

        _consumerControllers.Clear();

        var connection = _physicalConnection;
        if (connection != null)
        {
            try
            {
                if (connection.IsOpen)
                {
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to close connection");
            }
            finally
            {
                connection.Dispose();
            }
        }

        GC.SuppressFinalize(this);
    }

    private bool IsConnected()
    {
        if (_consumerControllers.Count == 0)
        {
            return true;
        }

        if (_physicalConnection == null || !_physicalConnection.IsOpen)
        {
            if (_physicalConnectionClosedSince == null)
            {
                _physicalConnectionClosedSince = DateTime.UtcNow;
                return true;
            }

            if (_physicalConnectionClosedSince != null && DateTime.UtcNow - _physicalConnectionClosedSince.Value > ConnectionClosedTolerance)
            {
                return false;
            }
        }

        _physicalConnectionClosedSince = null;

        return true;
    }

    private async Task<IConnection?> GetConnection(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var counter = 0;

        IConnection? connection = null;
        try
        {
            while (connection == null && !cancellationToken.IsCancellationRequested)
            {
                counter += 1;
                try
                {
                    connection = GetConnection();
                }
                catch
                {
                    connection = null;
                }

                if (connection != null)
                    return connection;

                if (timeout != Timeout.InfiniteTimeSpan && timeout > timer.Elapsed)
                {
                    break;
                }

                // ~ every 30 seconds
                if (counter % 6 == 0)
                {
                    _logger.LogWarning("RabbitMQ consumer connection is not ready after {Duration}", timer.Elapsed);
                }

                await Task
                    .Delay(TimeSpan.FromSeconds(5), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            // 
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Unable to open RabbitMQ connection. Check your configuration or increase server startup timeout. Giving up after {Duration}.", timer.Elapsed);
        }

        return connection;

    }

    private IConnection? GetConnection()
    {
        if (_physicalConnection == null)
        {
            lock (this)
            {
                if (_physicalConnection == null)
                {
                    var physicalConnection = _connection.Create(ConnectionType.Consumer);
                    _logger.LogWarning("Successfully created RabbitMQ consumer connection");

                    physicalConnection.CallbackException += OnPhysicalConnectionCallbackException;
                    physicalConnection.ConnectionBlocked += OnPhysicalConnectionBlocked;
                    physicalConnection.ConnectionShutdown += OnPhysicalConnectionShutdown;
                    physicalConnection.ConnectionUnblocked += OnPhysicalConnectionUnblocked;

                    _physicalConnection = physicalConnection;
                }
            }
        }

        return _physicalConnection;
    }

    private void OnPhysicalConnectionUnblocked(object? sender, EventArgs e)
    {
        _logger.LogWarning("Connection: Unblocked");
    }

    private void OnPhysicalConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning("Connection: Shutdown. Arguments: {ShutdownEventArs}.", e);
    }

    private void OnPhysicalConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        _logger.LogWarning("Connection: Blocked. Reason: {Reason}.", e.Reason);
    }

    private void OnPhysicalConnectionCallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        _logger.LogWarning(e.Exception, "Connection: Callback Exception");
    }

    private void TryCreateTopology(IConnection connection, StreamFlowDefaults? defaults, QueueOptions? queueOptions, string exchange, string queue, string routingKey)
    {
        var channel = connection.CreateModel();

        try
        {
            TryCreateExchange(exchange, channel, queueOptions?.ExchangeOptions);

            if (channel.IsClosed)
            {
                channel.Dispose();
                channel = connection.CreateModel();
            }

            channel.TryCreateQueue(_logger, defaults, queueOptions, queue);

            if (channel.IsClosed)
            {
                channel.Dispose();
                channel = connection.CreateModel();
            }

            TryCreateQueueAndExchangeBinding(exchange, queue, routingKey, channel);
        }
        finally
        {
            channel.Dispose();
        }
    }

    private void TryCreateExchange(string exchange, IModel channel, ExchangeOptions? exchangeOptions)
    {
        try
        {
            var durable = exchangeOptions?.Durable ?? true;
            var autoDelete = exchangeOptions?.AutoDelete ?? false;
            var arguments = exchangeOptions?.Arguments;

            _logger.LogInformation("Declaring exchange: [{ExchangeName}].", exchange);
            channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable, autoDelete, arguments);
        }
        catch (OperationInterruptedException e)
        {
            // 406: PRECONDITION_FAILED, exchange exists, but with different properties
            if (e.ShutdownReason?.ReplyCode != Constants.PreconditionFailed)
            {
                _logger.LogError(e, "Unable to declare exchange [{ExchangeName}]", exchange);
                throw;
            }

            _logger.LogWarning(e, "Exchange already exists with name [{ExchangeName}]", exchange);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to declare exchange [{ExchangeName}]", exchange);
            throw;
        }
    }

    private void TryCreateQueueAndExchangeBinding(string exchange, string queue, string routingKey, IModel channel)
    {
        try
        {
            _logger.LogInformation(
                "Binding exchange [{ExchangeName}] to queue [{QueueName}] using routing key [{RoutingKey}].", exchange,
                queue, routingKey);

            channel.QueueBind(queue, exchange, routingKey);
        }
        catch (OperationInterruptedException e)
        {
            // 406: PRECONDITION_FAILED, binding exists, but with different properties
            if (e.ShutdownReason?.ReplyCode != Constants.PreconditionFailed)
            {
                _logger.LogError(e,
                    "Unable to bind exchange [{ExchangeName}] to queue [{QueueName}] using routing key [{RoutingKey}].",
                    exchange, queue, routingKey);

                throw;
            }

            _logger.LogWarning(e,
                "Binding already exists for exchange [{ExchangeName}] to queue [{QueueName}] using routing key [{RoutingKey}].",
                exchange, queue, routingKey);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Unable to bind exchange [{ExchangeName}] to queue [{QueueName}] using routing key [{RoutingKey}].",
                exchange, queue, routingKey);

            throw;
        }
    }
}
