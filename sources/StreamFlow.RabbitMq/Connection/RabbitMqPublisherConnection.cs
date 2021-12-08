using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace StreamFlow.RabbitMq.Connection;

public interface IRabbitMqPublisherConnection
{
    RabbitMqPublisherOptions Options { get; }
    IConnection Get();
    RabbitMqChannel CreateChannel();
}

internal class RabbitMqPublisherConnection: IRabbitMqPublisherConnection, IDisposable
{
    private readonly ILogger<RabbitMqPublisherConnection> _logger;
    private readonly Lazy<IConnection> _connection;

    public RabbitMqPublisherConnection(RabbitMqPublisherOptions options, IRabbitMqConnection connection, ILogger<RabbitMqPublisherConnection> logger)
    {
        Options = options;
        _logger = logger;

        _connection = new Lazy<IConnection>(() =>
        {
            var physicalConnection = connection.Create();

            physicalConnection.CallbackException += OnPhysicalConnectionCallbackException;
            physicalConnection.ConnectionBlocked += OnPhysicalConnectionBlocked;
            physicalConnection.ConnectionShutdown += OnPhysicalConnectionShutdown;
            physicalConnection.ConnectionUnblocked += OnPhysicalConnectionUnblocked;

            return physicalConnection;
        });
    }

    public RabbitMqPublisherOptions Options { get; }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            var connection = _connection.Value;

            try
            {
                if (connection.IsOpen)
                {
                    connection.Close(Constants.ReplySuccess, "RabbitMqPublisherConnection is disposed.");
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to close RabbitMQ connection");
            }

            try
            {
                connection.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to dispose RabbitMQ connection");
            }
        }
    }

    public IConnection Get()
    {
        return _connection.Value;
    }

    public RabbitMqChannel CreateChannel()
    {
        var connection = _connection.Value;
        return new RabbitMqChannel(connection, Options.ConfirmationType);
    }

    private void OnPhysicalConnectionUnblocked(object? sender, EventArgs e)
    {
        _logger.LogWarning("PublisherConnection: Unblocked");
    }

    private void OnPhysicalConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning("PublisherConnection: Shutdown. Arguments: {ShutdownEventArs}.", e);
    }

    private void OnPhysicalConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        _logger.LogWarning("PublisherConnection: Blocked. Reason: {Reason}.", e.Reason);
    }

    private void OnPhysicalConnectionCallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        _logger.LogWarning(e.Exception, "PublisherConnection: Callback Exception");
    }
}
