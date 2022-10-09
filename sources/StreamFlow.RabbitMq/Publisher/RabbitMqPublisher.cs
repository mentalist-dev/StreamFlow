using System.Net;
using System.Text;

namespace StreamFlow.RabbitMq.Publisher;

public interface IRabbitMqPublisher
{
    void Publish<T>(T message, PublishOptions? options = null) where T : class;

    Task PublishAsync<T>(T message, PublishOptions? options = null, CancellationToken cancellationToken = default) where T : class;
}

internal class RabbitMqPublisher : IRabbitMqPublisher, IPublisher
{
    private static readonly string HostName = Dns.GetHostName();

    private readonly RabbitMqPublisherOptions _globalOptions;
    private readonly IRabbitMqPublicationQueue _queue;
    private readonly IRabbitMqConventions _conventions;
    private readonly IMessageSerializer _messageSerializer;
    private readonly IRabbitMqMetrics _metrics;

    public RabbitMqPublisher(RabbitMqPublisherOptions globalOptions, IRabbitMqPublicationQueue queue, IRabbitMqConventions conventions, IMessageSerializer messageSerializer, IRabbitMqMetrics metrics)
    {
        _globalOptions = globalOptions;
        _queue = queue;
        _conventions = conventions;
        _messageSerializer = messageSerializer;
        _metrics = metrics;
    }

    public void Publish<T>(T message, PublishOptions? options = null) where T : class
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        var exchange = options?.Exchange ?? _conventions.GetExchangeName(message.GetType());

        // will measure the whole pipeline
        var totalDuration = _metrics.Published(exchange);

        // will measure only this method
        using var duration = _metrics.PublicationCreated(exchange);

        var routingKey = options?.RoutingKey;
        if (string.IsNullOrWhiteSpace(routingKey))
        {
            routingKey = "#";
        }

        ReadOnlyMemory<byte> body;

        if (message is byte[] buffer)
        {
            body = buffer;
        }
        else if (message is ReadOnlyMemory<byte> memoryBuffer)
        {
            body = memoryBuffer;
        }
        else if (message is string text)
        {
            body = Encoding.UTF8.GetBytes(text);
        }
        else
        {
            body = _messageSerializer.Serialize(message);
        }

        var contentType = _messageSerializer.GetContentType();

        var correlationId = options?.CorrelationId;
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        var context = new RabbitMqPublisherMessageContext(body, _globalOptions, options);
        context
            .WithCorrelationId(correlationId)
            .WithRoutingKey(routingKey)
            .WithExchange(exchange)
            .WithContentType(contentType)
            .SetHeader("SF:PublishTime", DateTime.UtcNow.ToString("O"))
            .SetHeader("SF:PublisherHostName", HostName);

        var headers = options?.Headers;
        if (headers != null)
        {
            foreach (var header in headers)
            {
                context.SetHeader(header.Key, header.Value);
            }
        }

        var messageCompletion = new RabbitMqPublication(totalDuration, context, CancellationToken.None, options?.Timeout, true);

        _queue.Publish(messageCompletion);

        duration?.Complete();
    }

    public Task PublishAsync<T>(T message, PublishOptions? options = null, CancellationToken cancellationToken = default) where T : class
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        var exchange = options?.Exchange ?? _conventions.GetExchangeName(message.GetType());

        // will measure the whole pipeline
        var totalDuration = _metrics.Published(exchange);

        // will measure only this method
        using var duration = _metrics.PublicationCreated(exchange);

        var routingKey = options?.RoutingKey;
        if (string.IsNullOrWhiteSpace(routingKey))
        {
            routingKey = "#";
        }

        ReadOnlyMemory<byte> body;

        if (message is byte[] buffer)
        {
            body = buffer;
        }
        else if (message is ReadOnlyMemory<byte> memoryBuffer)
        {
            body = memoryBuffer;
        }
        else if (message is string text)
        {
            body = Encoding.UTF8.GetBytes(text);
        }
        else
        {
            body = _messageSerializer.Serialize(message);
        }

        var contentType = _messageSerializer.GetContentType();

        var correlationId = options?.CorrelationId;
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        var context = new RabbitMqPublisherMessageContext(body, _globalOptions, options);
        context
            .WithCorrelationId(correlationId)
            .WithRoutingKey(routingKey)
            .WithExchange(exchange)
            .WithContentType(contentType)
            .SetHeader("SF:PublishTime", DateTime.UtcNow.ToString("O"))
            .SetHeader("SF:PublisherHostName", HostName);

        var headers = options?.Headers;
        if (headers != null)
        {
            foreach (var header in headers)
            {
                context.SetHeader(header.Key, header.Value);
            }
        }

        var messageCompletion = new RabbitMqPublication(totalDuration, context, cancellationToken, options?.Timeout);

        _queue.Publish(messageCompletion);

        duration?.Complete();

        return messageCompletion.Completion.Task;
    }
}
