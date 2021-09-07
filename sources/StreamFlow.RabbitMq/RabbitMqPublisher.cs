using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using StreamFlow.Pipes;
using StreamFlow.RabbitMq.Connection;

namespace StreamFlow.RabbitMq
{
    public class RabbitMqPublisher : IPublisher, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly IStreamFlowPublisherPipe _pipe;
        private readonly IRabbitMqConventions _conventions;
        private readonly IRabbitMqMetrics _metrics;
        private readonly IMessageSerializer _messageSerializer;
        private readonly ILogger<RabbitMqPublisher> _logger;
        private readonly Lazy<RabbitMqChannel> _model;

        public RabbitMqPublisher(IServiceProvider services
            , IStreamFlowPublisherPipe pipe
            , IMessageSerializer messageSerializer
            , IRabbitMqConnection connection
            , IRabbitMqConventions conventions
            , IRabbitMqMetrics metrics
            , ILogger<RabbitMqPublisher> logger)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            _services = services;
            _conventions = conventions;
            _metrics = metrics;
            _pipe = pipe;
            _messageSerializer = messageSerializer;
            _logger = logger;
            _model = new Lazy<RabbitMqChannel>(() => new RabbitMqChannel(connection.Create()));
        }

        protected IModel Channel => _model.Value.Channel;

        public async Task PublishAsync<T>(T message, PublishOptions? options = null, CancellationToken cancellationToken = default) where T: class
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var exchange = options?.TargetAddress;
            if (exchange == null)
            {
                exchange = _conventions.GetExchangeName(message.GetType());
            }

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
                .SetHeader("SF:PublishTime", DateTime.UtcNow.ToString("O"));

            var headers = options?.Headers;
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    context.SetHeader(header.Key, header.Value);
                }
            }

            await _pipe.ExecuteAsync(_services, context, messageContext =>
            {
                var publisherConfirmsEnabled = options?.PublisherConfirmsEnabled ?? false;
                var publisherConfirmsTimeout = options?.PublisherConfirmsTimeout;

                Publish(messageContext, isMandatory, publisherConfirmsEnabled, publisherConfirmsTimeout);
                return Task.CompletedTask;
            }).ConfigureAwait(false);

            _metrics.MessagePublished(exchange);
        }

        private void Publish(IMessageContext message, bool isMandatory, bool enablePublisherConfirms, TimeSpan? publisherConfirmsTimeout)
        {
            var properties = Channel.CreateBasicProperties();

            message.MapTo(properties);

            _logger.LogDebug(
                "Publishing message to exchange [{ExchangeName}] using routing key [{RoutingKey}] with CorrelationId [{CorrelationId}]",
                message.Exchange, message.RoutingKey, message.CorrelationId);

            Channel.BasicPublish(message.Exchange, message.RoutingKey, isMandatory, properties, message.Content);

            if (enablePublisherConfirms)
            {
                if (publisherConfirmsTimeout > TimeSpan.Zero)
                {
                    Channel.WaitForConfirms(publisherConfirmsTimeout.Value);
                }
                else
                {
                    Channel.WaitForConfirms();
                }
            }
        }

        public void Dispose()
        {
            if (_model.IsValueCreated)
            {
                var value = _model.Value;
                value.Dispose();
            }
        }
    }
}
