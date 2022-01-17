using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using StreamFlow.Pipes;
using StreamFlow.RabbitMq.Connection;

namespace StreamFlow.RabbitMq;

internal class RabbitMqPublisher : IPublisher, IDisposable
{
    private static readonly string HostName = Dns.GetHostName();

    private readonly object _lock = new();
    private readonly IServiceProvider _services;
    private readonly IStreamFlowPublisherPipe _pipe;
    private readonly IRabbitMqPublisherConnection _connection;
    private readonly IMessageSerializer _messageSerializer;
    private readonly IRabbitMqConventions _conventions;
    private readonly IRabbitMqMetrics _metrics;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly Func<IMessageContext, PublishRequest, Task> _publish;

    private int _disposed;
    private RabbitMqChannel? _channel;

    public RabbitMqPublisher(IServiceProvider services
        , IStreamFlowPublisherPipe pipe
        , IRabbitMqPublisherConnection connection
        , IMessageSerializer messageSerializer
        , IRabbitMqConventions conventions
        , IRabbitMqMetrics metrics
        , ILogger<RabbitMqPublisher> logger)
    {
        _services = services;
        _pipe = pipe;
        _connection = connection;
        _messageSerializer = messageSerializer;
        _conventions = conventions;
        _metrics = metrics;
        _logger = logger;

        _publish = (message, request) =>
        {
            var timer = Stopwatch.StartNew();

            var options = request.Options;
            var waitForConfirmation = options?.WaitForConfirmation ?? false;
            var waitForConfirmationTimeout = options?.WaitForConfirmationTimeout;
            var metricsPrefix = options?.MetricsPrefix ?? string.Empty;

            var isMandatory = request.IsMandatory;

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Publishing message to exchange [{ExchangeName}] using routing key [{RoutingKey}] with CorrelationId [{CorrelationId}]",
                    message.Exchange, message.RoutingKey, message.CorrelationId);
            }

            var exchangeName = message.Exchange ?? string.Empty;

            _metrics.PublishingEvent(exchangeName, $"{metricsPrefix}channel:prepare", timer.Elapsed);
            timer.Restart();

            var channel = GetChannel(exchangeName, metricsPrefix, _metrics);

            _metrics.PublishingEvent(exchangeName, $"{metricsPrefix}channel:get", timer.Elapsed);
            timer.Restart();

            request.Response = channel.Publish(message, isMandatory, waitForConfirmation, waitForConfirmationTimeout, _metrics);

            _metrics.PublishingEvent(exchangeName, $"{metricsPrefix}channel:publish", timer.Elapsed);
            timer.Stop();

            return Task.CompletedTask;
        };
    }

    public void Dispose()
    {
        var disposed = Interlocked.Increment(ref _disposed);
        if (disposed > 1)
            return;

        var channel = _channel;
        if (channel != null)
        {
            try
            {
                if (channel.IsOpen)
                {
                    channel.Close(Constants.ReplySuccess, "RabbitMqPublisherChannel disposed.");
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to close publisher channel");
            }

            try
            {
                channel.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to dispose publisher channel");
            }
        }

        GC.SuppressFinalize(this);
    }

    public async Task<PublishResponse> PublishAsync<T>(T message, PublishOptions? options = null) where T: class
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        var exchange = options?.TargetAddress ?? _conventions.GetExchangeName(message.GetType());

        var busPublisher = options?.BusPublisher ?? false;
        var metricsPrefix = options?.MetricsPrefix ?? string.Empty;

        using var duration = _metrics.Publishing(exchange, busPublisher);
        var timer = Stopwatch.StartNew();

        try
        {
            var routingKey = options?.RoutingKey;
            if (string.IsNullOrWhiteSpace(routingKey))
            {
                routingKey = "#";
            }

            var isMandatory = options?.IsMandatory ?? false;

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

            _metrics.PublishingEvent(exchange, $"{metricsPrefix}serialization", timer.Elapsed);
            timer.Restart();

            var contentType = _messageSerializer.GetContentType<T>();

            var correlationId = options?.CorrelationId;
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            var context = new RabbitMqPublisherMessageContext(body)
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

            _metrics.PublishingEvent(exchange, $"{metricsPrefix}message", timer.Elapsed);
            timer.Restart();

            var request = new PublishRequest(options, isMandatory);

            await _pipe
                .ExecuteAsync(_services, context, msg => _publish(msg, request))
                .ConfigureAwait(false);

            var response = request.Response ?? new PublishResponse(null);

            _metrics.PublishingEvent(exchange, $"{metricsPrefix}publish", timer.Elapsed);
            timer.Stop();

            duration?.Complete();

            return response;
        }
        catch (Exception e)
        {
            _metrics.PublishingError(exchange, busPublisher);
            _logger.LogError(e, "Unable to publish message to {Exchange}", exchange);
            throw;
        }
    }

    private RabbitMqChannel GetChannel(string exchangeName, string metricsPrefix, IRabbitMqMetrics metrics)
    {
        if (_channel == null)
        {
            var timer = Stopwatch.StartNew();
            lock (_lock)
            {
                metrics.PublishingEvent(exchangeName, $"{metricsPrefix}channel:lock", timer.Elapsed);
                timer.Restart();

                if (_channel == null)
                {
                    _channel = _connection.CreateChannel();
                    metrics.PublishingEvent(exchangeName, $"{metricsPrefix}channel:create", timer.Elapsed);
                }
            }
        }

        return _channel;
    }

    private class PublishRequest
    {
        public PublishOptions? Options { get; }
        public bool IsMandatory { get; }

        public PublishResponse? Response { get; set; }

        public PublishRequest(PublishOptions? options, bool isMandatory)
        {
            Options = options;
            IsMandatory = isMandatory;
        }
    }
}
