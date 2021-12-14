using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using StreamFlow.Configuration;
using StreamFlow.RabbitMq.Connection;
using StreamFlow.Server;

namespace StreamFlow.RabbitMq.Server;

public interface IRabbitMqServer
{
    Task Start(IConsumerRegistration consumerRegistration, TimeSpan timeout, CancellationToken cancellationToken);
    void Stop();
}

public class RabbitMqServer: IRabbitMqServer, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IRabbitMqConventions _conventions;
    private readonly Lazy<IConnection> _connection;
    private readonly List<RabbitMqConsumerController> _consumerControllers = new();
    private readonly ILogger<RabbitMqConsumer> _logger;

    public RabbitMqServer(IServiceProvider services, IRabbitMqConnection connection, IRabbitMqConventions conventions, ILoggerFactory loggers)
    {
        _logger = loggers.CreateLogger<RabbitMqConsumer>();

        _services = services;
        _conventions = conventions;
        _connection = new Lazy<IConnection>(() =>
        {
            _logger.LogWarning("Creating consumer RabbitMQ connection");
            var physicalConnection = connection.Create(ConnectionType.Consumer);

            physicalConnection.CallbackException += OnPhysicalConnectionCallbackException;
            physicalConnection.ConnectionBlocked += OnPhysicalConnectionBlocked;
            physicalConnection.ConnectionShutdown += OnPhysicalConnectionShutdown;
            physicalConnection.ConnectionUnblocked += OnPhysicalConnectionUnblocked;

            return physicalConnection;
        });
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

    public async Task Start(IConsumerRegistration consumerRegistration, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var requestType = consumerRegistration.RequestType;
        var consumerType = consumerRegistration.ConsumerType;
        var consumerGroup = consumerRegistration.Options.ConsumerGroup;
        var defaults = consumerRegistration.Default;

        var connection = _connection.Value;

        if (!connection.IsOpen)
        {
            var timer = Stopwatch.StartNew();
            var counter = 0;
            while (!connection.IsOpen)
            {
                counter += 1;

                if (timeout != Timeout.InfiniteTimeSpan && timeout > timer.Elapsed)
                {
                    throw new Exception($"Unable to start consumer as connection was not open after {timer.Elapsed}");
                }

                // ~ every 30 seconds
                if (counter % 6 == 0)
                {
                    _logger.LogWarning("RabbitMQ connection is still not ready after {Duration}", timer.Elapsed);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

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

        for (int i = 0; i < consumerCount; i++)
        {
            var consumerInfo = new RabbitMqConsumerInfo(exchange, queue, routingKey);

            var controller = new RabbitMqConsumerController(_services, consumerRegistration, consumerInfo, connection, _logger, cancellationToken);
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

        if (_connection.IsValueCreated)
        {
            try
            {
                if (_connection.Value.IsOpen)
                {
                    _connection.Value.Close();
                }
            }
            finally
            {
                _connection.Value.Dispose();
            }
        }

        GC.SuppressFinalize(this);
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
