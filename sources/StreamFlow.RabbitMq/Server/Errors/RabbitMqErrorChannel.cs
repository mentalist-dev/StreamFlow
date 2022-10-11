using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace StreamFlow.RabbitMq.Server.Errors;

internal sealed class RabbitMqErrorChannel : IDisposable
{
    private readonly ILogger _logger;

    public Guid Id { get; } = Guid.NewGuid();
    public IModel Channel { get; }

    public RabbitMqErrorChannel(IConnection connection, ILogger logger)
    {
        _logger = logger;
        Channel = connection.CreateModel();

        Channel.BasicRecoverOk += OnRecover;
        Channel.CallbackException += OnCallbackException;
        Channel.FlowControl += OnFlowControl;
        Channel.ModelShutdown += OnModelShutdown;
        Channel.TxSelect();
    }

    ~RabbitMqErrorChannel()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                var channel = Channel;

                Channel.BasicRecoverOk -= OnRecover;
                Channel.CallbackException -= OnCallbackException;
                Channel.FlowControl -= OnFlowControl;
                Channel.ModelShutdown -= OnModelShutdown;

                channel.Dispose();
            }
            catch
            {
                //
            }
        }
    }

    public void Send(ReadOnlyMemory<byte> body, IBasicProperties properties, string queueName)
    {
        var exchange = string.Empty;
        var routingKey = queueName;

        Channel.BasicPublish(exchange, routingKey, true, properties, body);
        Channel.TxCommit();
    }

    private void OnFlowControl(object? sender, FlowControlEventArgs e)
    {
        _logger.LogWarning("Flow control active: {Active}", e.Active);
    }

    private void OnCallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        _logger.LogDebug(e.Exception, "Callback exception: {@CallbackExceptionDetail}", e.Detail);
    }

    private void OnRecover(object? sender, EventArgs e)
    {
        _logger.LogDebug("Channel recovered");
    }

    private void OnModelShutdown(object? sender, ShutdownEventArgs e)
    {
        _logger.LogDebug("Channel shutdown");
    }
}
