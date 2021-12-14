using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace StreamFlow.RabbitMq.Connection;

internal class RabbitMqChannel : IDisposable
{
    private readonly ConcurrentQueue<BasicAckEventArgs> _acknowledged = new();
    private readonly ConcurrentQueue<BasicNackEventArgs> _rejected = new();

    private readonly Channel<BasicReturnEventArgs> _returned = System.Threading.Channels.Channel.CreateBounded<BasicReturnEventArgs>(10000);
    private readonly ConfirmationType? _confirmation;

    public RabbitMqChannel(IConnection connection, ConfirmationType? confirmationType)
    {
        Channel = connection.CreateModel();
        _confirmation = confirmationType;

        switch (confirmationType)
        {
            case ConfirmationType.PublisherConfirms:
                Channel.ConfirmSelect();

                Channel.BasicAcks += OnAcknowledge;
                Channel.BasicNacks += OnReject;
                Channel.BasicReturn += OnReturn;

                break;
            case ConfirmationType.Transactional:
                Channel.TxSelect();
                break;
            case null:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(confirmationType), confirmationType, null);
        }
    }

    public Guid Id { get; } = Guid.NewGuid();
    public IModel Channel { get; }

    public bool IsOpen => Channel.IsOpen;

    internal event EventHandler<EventArgs>? Disposed;

    public void Close(ushort replyCode, string replyText)
    {
        Channel.Close(replyCode, replyText);
    }

    public void Dispose()
    {
        _returned.Writer.TryComplete();

        if (_confirmation == ConfirmationType.PublisherConfirms)
        {
            Channel.BasicAcks -= OnAcknowledge;
            Channel.BasicNacks -= OnReject;
            Channel.BasicReturn -= OnReturn;
        }

        Channel.Dispose();

        Disposed?.Invoke(this, EventArgs.Empty);

        GC.SuppressFinalize(this);
    }

    public PublishResponse Publish(IMessageContext message, bool isMandatory, bool waitForConfirmation, TimeSpan? waitForConfirmationTimeout, IRabbitMqMetrics metrics)
    {
        var timer = Stopwatch.StartNew();

        var response = new PublishResponse(
            _confirmation == ConfirmationType.PublisherConfirms ? Channel.NextPublishSeqNo : null
        );

        var properties = Channel.CreateBasicProperties();
        message.MapTo(properties);

        var exchange = message.Exchange ?? string.Empty;
        var routingKey = message.RoutingKey;
        if (string.IsNullOrWhiteSpace(routingKey))
            routingKey = "#";

        var body = message.Content;

        metrics.PublishingEvent(exchange, "channel:properties", timer.Elapsed);
        timer.Restart();

        Channel.BasicPublish(exchange, routingKey, isMandatory, properties, body);

        metrics.PublishingEvent(exchange, "channel:basic-publish", timer.Elapsed);
        timer.Restart();

        Commit(waitForConfirmation, waitForConfirmationTimeout);

        metrics.PublishingEvent(exchange, "channel:commit", timer.Elapsed);
        timer.Restart();

        if (_confirmation == ConfirmationType.PublisherConfirms)
        {
            while (_acknowledged.TryDequeue(out var ack))
            {
                response.Acknowledged(ack.DeliveryTag, ack.Multiple);
            }

            metrics.PublishingEvent(exchange, "channel:ack-collect", timer.Elapsed);
            timer.Restart();

            while (_rejected.TryDequeue(out var nack))
            {
                response.Rejected(nack.DeliveryTag, nack.Multiple);
            }

            metrics.PublishingEvent(exchange, "channel:nack-collect", timer.Elapsed);
            timer.Restart();
        }

        return response;
    }

    public PublishResponse Send(ReadOnlyMemory<byte> body, IBasicProperties properties, string queueName, TimeSpan? waitForConfirmationTimeout, IRabbitMqMetrics metrics)
    {
        var timer = Stopwatch.StartNew();

        var response = new PublishResponse(
            _confirmation == ConfirmationType.PublisherConfirms ? Channel.NextPublishSeqNo : null
        );

        Channel.BasicPublish(string.Empty, queueName, true, properties, body);

        const string exchangeName = "(AMQP default)";
        metrics.PublishingEvent(exchangeName, "channel:basic-publish", timer.Elapsed);
        timer.Restart();

        Commit(true, waitForConfirmationTimeout);

        metrics.PublishingEvent(exchangeName, "channel:commit", timer.Elapsed);
        timer.Restart();

        if (_confirmation == ConfirmationType.PublisherConfirms)
        {
            while (_acknowledged.TryDequeue(out var ack))
            {
                response.Acknowledged(ack.DeliveryTag, ack.Multiple);
            }

            metrics.PublishingEvent(exchangeName, "channel:ack-collect", timer.Elapsed);
            timer.Restart();

            while (_rejected.TryDequeue(out var nack))
            {
                response.Rejected(nack.DeliveryTag, nack.Multiple);
            }

            metrics.PublishingEvent(exchangeName, "channel:nack-collect", timer.Elapsed);
            timer.Restart();
        }

        return response;
    }

    private void Commit(bool waitForConfirmation, TimeSpan? timeout = null)
    {
        switch (_confirmation)
        {
            case ConfirmationType.PublisherConfirms:
                if (waitForConfirmation)
                {
                    timeout ??= Timeout.InfiniteTimeSpan;
                    Channel.WaitForConfirms(timeout.Value);
                }

                break;

            case ConfirmationType.Transactional:
                Channel.TxCommit();
                break;

            case null:
                break;
        }
    }

    void OnAcknowledge(object? sender, BasicAckEventArgs args)
    {
        _acknowledged.Enqueue(args);
    }

    void OnReject(object? sender, BasicNackEventArgs args)
    {
        _rejected.Enqueue(args);
    }

    private void OnReturn(object? sender, BasicReturnEventArgs args)
    {
        _returned.Writer.TryWrite(args);
    }
}
