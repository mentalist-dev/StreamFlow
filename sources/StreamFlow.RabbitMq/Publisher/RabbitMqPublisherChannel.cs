using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace StreamFlow.RabbitMq.Publisher;

internal sealed class RabbitMqPublisherChannel : IDisposable
{
    private const string PublicationHeaderName = "SF:PublicationId";
    private readonly ConcurrentDictionary<ulong, PublishedMessage> _completions = new();
    private readonly ConcurrentDictionary<string, PublishedMessage> _messages = new();
    private readonly ILogger _logger;

    private ulong _lastSequenceNo;

    public Guid Id { get; } = Guid.NewGuid();
    public IModel Channel { get; }

    public RabbitMqPublisherChannel(IConnection connection, ILogger logger)
    {
        _logger = logger;
        Channel = connection.CreateModel();

        Channel.BasicAcks += OnAcknowledge;
        Channel.BasicNacks += OnReject;
        Channel.BasicReturn += OnReturn;
        Channel.BasicRecoverOk += OnRecover;
        Channel.CallbackException += OnCallbackException;
        Channel.FlowControl += OnFlowControl;
        Channel.ModelShutdown += OnModelShutdown;

        Channel.ConfirmSelect();
    }

    ~RabbitMqPublisherChannel()
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

                Channel.BasicAcks -= OnAcknowledge;
                Channel.BasicNacks -= OnReject;
                Channel.BasicReturn -= OnReturn;
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

    public Task PublishAsync(RabbitMqPublication publication)
    {
        var context = publication.Context;
        var publicationId = Guid.NewGuid().ToString();

        var properties = Channel.CreateBasicProperties();
        context.MapTo(properties);

        properties.Headers ??= new Dictionary<string, object>();
        properties.Headers[PublicationHeaderName] = publicationId;

        var exchange = context.Exchange ?? string.Empty;

        var routingKey = context.RoutingKey;
        if (string.IsNullOrWhiteSpace(routingKey))
            routingKey = "#";

        var body = context.Content;

        var publishSeqNo = Channel.NextPublishSeqNo;

        var lastSeqNo = Interlocked.Exchange(ref _lastSequenceNo, publishSeqNo);
        if (publishSeqNo <= lastSeqNo)
        {
            var sequences = _completions.Where(p => p.Key <= lastSeqNo).Select(p => p.Key);
            foreach (var sequenceNo in sequences)
            {
                if (_completions.TryRemove(sequenceNo, out var item))
                {
                    _messages.TryRemove(item.Id, out _);
                    item.Publication.Fail(new RabbitMqPublisherException(PublicationExceptionReason.ChannelReset));
                }
            }
        }

        var message = new PublishedMessage(publicationId, publishSeqNo, publication);

        try
        {
            _completions[publishSeqNo] = message;
            _messages[publicationId] = message;

            Channel.BasicPublish(exchange, routingKey, true, properties, body);
        }
        catch
        {
            _completions.TryRemove(publishSeqNo, out _);
            _messages.TryRemove(publicationId, out _);
            throw;
        }

        return Task.CompletedTask;
    }

    private void OnAcknowledge(object? sender, BasicAckEventArgs e)
    {
        if (e.Multiple)
        {
            var sequences = _completions.Where(p => p.Key <= e.DeliveryTag).Select(p => p.Key);
            foreach (var sequenceNo in sequences)
            {
                OnAcknowledge(sequenceNo);
            }
        }
        else
        {
            OnAcknowledge(e.DeliveryTag);
        }
    }

    private void OnAcknowledge(ulong sequenceNo)
    {
        if (_completions.TryRemove(sequenceNo, out var item))
        {
            _messages.TryRemove(item.Id, out _);
            item.Publication.Complete();
        }
    }

    private void OnReject(object? sender, BasicNackEventArgs e)
    {
        if (e.Multiple)
        {
            var sequences = _completions.Where(p => p.Key <= e.DeliveryTag).Select(p => p.Key);
            foreach (var sequenceNo in sequences)
            {
                OnReject(sequenceNo);
            }
        }
        else
        {
            OnReject(e.DeliveryTag);
        }
    }

    private void OnReject(ulong sequenceNo)
    {
        if (_completions.TryRemove(sequenceNo, out var item))
        {
            _messages.TryRemove(item.Id, out _);
            item.Publication.Fail(new RabbitMqPublisherException(PublicationExceptionReason.Rejected));
        }
    }

    private void OnReturn(object? sender, BasicReturnEventArgs e)
    {
        var headers = e.BasicProperties?.Headers;
        if (headers != null && headers.TryGetValue(PublicationHeaderName, out var value))
        {
            string publicationId = string.Empty;
            if (value is byte[] bytes)
            {
                publicationId = Encoding.UTF8.GetString(bytes);
            }
            else if (value is string id)
            {
                publicationId = id;
            }

            if (!string.IsNullOrWhiteSpace(publicationId) && _messages.TryRemove(publicationId, out var item))
            {
                _completions.TryRemove(item.SequenceNo, out _);
                item.Publication.Fail(new RabbitMqPublisherException(PublicationExceptionReason.Returned));
            }
        }
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
        foreach (var cp in _completions)
        {
            var reason = e.ReplyCode == Constants.NotFound
                ? PublicationExceptionReason.ExchangeNotFound
                : PublicationExceptionReason.ChannelReset;

            if (reason == PublicationExceptionReason.ExchangeNotFound && cp.Value.Publication.Context.IgnoreNoRoutes)
            {
                cp.Value.Publication.Complete();
            }
            else
            {
                cp.Value.Publication.Fail(new RabbitMqPublisherException(reason));
            }
        }

        _completions.Clear();
    }

    private sealed class PublishedMessage
    {
        public string Id { get; }
        public ulong SequenceNo { get; }
        public RabbitMqPublication Publication { get; }

        public PublishedMessage(string id, ulong sequenceNo, RabbitMqPublication publication)
        {
            Id = id;
            SequenceNo = sequenceNo;
            Publication = publication;
        }
    }
}
