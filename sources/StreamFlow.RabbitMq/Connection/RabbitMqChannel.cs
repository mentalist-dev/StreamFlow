using System.Collections.Concurrent;
using System.Threading.Channels;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace StreamFlow.RabbitMq.Connection;

public class RabbitMqChannel : IDisposable
{
    private readonly ConcurrentQueue<BasicAckEventArgs> _acknowledged = new();
    private readonly ConcurrentQueue<BasicNackEventArgs> _rejected = new();

    private readonly Channel<BasicReturnEventArgs> _returned = System.Threading.Channels.Channel.CreateBounded<BasicReturnEventArgs>(10000);

    public RabbitMqChannel(IConnection connection, ConfirmationType? confirmationType)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Channel = connection.CreateModel();
        Confirmation = confirmationType;

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

    public IConnection Connection { get; }
    public IModel Channel { get; }
    public ConfirmationType? Confirmation { get; }

    public bool IsOpen => Channel.IsOpen;

    public void Close(ushort replyCode, string replyText)
    {
        Channel.Close(replyCode, replyText);
    }

    public void Dispose()
    {
        _returned.Writer.Complete();

        if (Confirmation == ConfirmationType.PublisherConfirms)
        {
            Channel.BasicAcks -= OnAcknowledge;
            Channel.BasicNacks -= OnReject;
            Channel.BasicReturn -= OnReturn;
        }

        Channel.Dispose();
    }

    public PublishResponse Publish(IMessageContext message, bool isMandatory, bool waitForConfirmation, TimeSpan? waitForConfirmationTimeout)
    {
        var response = new PublishResponse(
            Confirmation == ConfirmationType.PublisherConfirms ? Channel.NextPublishSeqNo : null
        );

        var properties = Channel.CreateBasicProperties();
        message.MapTo(properties);

        var exchange = message.Exchange ?? string.Empty;
        var routingKey = message.RoutingKey;
        if (string.IsNullOrWhiteSpace(routingKey))
            routingKey = "#";

        var body = message.Content;

        Channel.BasicPublish(exchange, routingKey, isMandatory, properties, body);
        Commit(waitForConfirmation, waitForConfirmationTimeout);

        while (_acknowledged.TryDequeue(out var ack))
        {
            response.Acknowledged(ack.DeliveryTag, ack.Multiple);
        }

        while (_rejected.TryDequeue(out var nack))
        {
            response.Rejected(nack.DeliveryTag, nack.Multiple);
        }

        return response;
    }

    public void Commit(bool waitForConfirmation, TimeSpan? timeout = null)
    {
        switch (Confirmation)
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
