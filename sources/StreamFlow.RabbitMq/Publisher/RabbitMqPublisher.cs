using System.Net;
using System.Text;
using StreamFlow.Pipes;

namespace StreamFlow.RabbitMq.Publisher;

public interface IRabbitMqPublisher: IPublisher
{
}

internal sealed class RabbitMqPublisher : IRabbitMqPublisher
{
    private static readonly string HostName = Dns.GetHostName();

    private readonly RabbitMqPublisherOptions _globalOptions;
    private readonly IStreamFlowPublisherPipe _pipe;
    private readonly IRabbitMqPublicationQueue _queue;
    private readonly IRabbitMqConventions _conventions;
    private readonly IMessageSerializer _messageSerializer;
    private readonly IRabbitMqMetrics _metrics;
    private readonly IServiceProvider _services;
    private readonly IRabbitMqPublisherHost _host;

    public RabbitMqPublisher(RabbitMqPublisherOptions globalOptions
        , IStreamFlowPublisherPipe pipe
        , IRabbitMqPublicationQueue queue
        , IRabbitMqConventions conventions
        , IMessageSerializer messageSerializer
        , IRabbitMqMetrics metrics
        , IServiceProvider services
        , IRabbitMqPublisherHost host)
    {
        _globalOptions = globalOptions;
        _pipe = pipe;
        _queue = queue;
        _conventions = conventions;
        _messageSerializer = messageSerializer;
        _metrics = metrics;
        _services = services;
        _host = host;
    }

    public Task PublishAsync<T>(T message, PublishOptions? options = null, CancellationToken cancellationToken = default) where T : class
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        return PublishInternalAsync(message, options, cancellationToken);
    }

    private Task PublishInternalAsync<T>(T message, PublishOptions? options, CancellationToken cancellationToken) where T : class
    {
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

        var task = _pipe.ExecuteAsync(_services, context, ctx => PublishInternalAsync(ctx, options, totalDuration, cancellationToken));

        duration?.Complete();
        return task;
    }

    private Task PublishInternalAsync(IMessageContext ctx, PublishOptions? options, IDurationMetric? totalDuration, CancellationToken cancellationToken)
    {
        var messageCompletion = new RabbitMqPublication(totalDuration
            , (RabbitMqPublisherMessageContext) ctx
            , cancellationToken
            , options?.Timeout,
            _metrics.PublisherQueued(ctx.Exchange ?? string.Empty)
        );

        if (_host.IsRunning)
        {
            _queue.Publish(messageCompletion);
        }
        else
        {
            messageCompletion.Fail(new RabbitMqPublisherException(PublicationExceptionReason.PublisherStopped));
        }
        
        return messageCompletion.Completion.Task;
    }
}
