using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StreamFlow.RabbitMq.Server;

namespace StreamFlow.RabbitMq.Publisher;

internal interface IRabbitMqPublisherConnection
{
    IConnection Get();

    bool? IsConnected { get; }
}

internal sealed class RabbitMqPublisherConnection: IRabbitMqPublisherConnection, IDisposable
{
    private readonly IRabbitMqConnection _connection;
    private readonly ILogger<RabbitMqPublisherConnection> _logger;
    private readonly object _lock = new();
    private IConnection? _physicalConnection;

    public RabbitMqPublisherConnection(IRabbitMqConnection connection, ILogger<RabbitMqPublisherConnection> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public void Dispose()
    {
        var connection = _physicalConnection;
        if (connection != null)
        {
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
        if (_physicalConnection == null)
        {
            lock (_lock)
            {
                if (_physicalConnection == null)
                {
                    _logger.LogWarning("Creating publisher RabbitMQ connection");

                    var physicalConnection = _connection.Create(ConnectionType.Publisher);

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

    public bool? IsConnected => _physicalConnection?.IsOpen;

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
