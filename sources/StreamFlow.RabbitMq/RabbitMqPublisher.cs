using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StreamFlow.Pipes;

namespace StreamFlow.RabbitMq
{
    public class RabbitMqPublisher : IPublisher
    {
        private readonly IServiceProvider _services;
        private readonly IStreamFlowPublisherPipe _pipe;
        private readonly IRabbitMqConventions _conventions;
        private readonly IRabbitMqMetrics _metrics;
        private readonly IMessageSerializer _messageSerializer;
        private readonly IRabbitMqChannelPool _channels;
        private readonly ILogger<RabbitMqPublisher> _logger;

        public RabbitMqPublisher(IServiceProvider services
            , IStreamFlowPublisherPipe pipe
            , IMessageSerializer messageSerializer
            , IRabbitMqChannelPool channels
            , IRabbitMqConventions conventions
            , IRabbitMqMetrics metrics
            , ILogger<RabbitMqPublisher> logger)
        {
            _services = services;
            _conventions = conventions;
            _metrics = metrics;
            _pipe = pipe;
            _messageSerializer = messageSerializer;
            _channels = channels;
            _logger = logger;
        }

        public async Task PublishAsync<T>(T message, PublishOptions? options = null, CancellationToken cancellationToken = default) where T: class
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var exchange = options?.TargetAddress;
            if (exchange == null)
            {
                exchange = _conventions.GetExchangeName(message.GetType());
            }

            using var _ = _metrics.MessageInPublishing(exchange);

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

            await _pipe
                .ExecuteAsync(_services, context, GetPublishAction(options, isMandatory))
                .ConfigureAwait(false);
        }

        private Func<IMessageContext, Task> GetPublishAction(PublishOptions? options, bool isMandatory)
        {
            return context =>
            {
                var publisherConfirmsEnabled = options?.PublisherConfirmsEnabled ?? false;
                var publisherConfirmsTimeout = options?.PublisherConfirmsTimeout;

                Publish(context, isMandatory, publisherConfirmsEnabled, publisherConfirmsTimeout);
                return Task.CompletedTask;
            };
        }

        private void Publish(IMessageContext message, bool isMandatory, bool enablePublisherConfirms, TimeSpan? publisherConfirmsTimeout)
        {
            var model = _channels.Get();
            try
            {
                var properties = model.Channel.CreateBasicProperties();

                message.MapTo(properties);

                _logger.LogDebug(
                    "Publishing message to exchange [{ExchangeName}] using routing key [{RoutingKey}] with CorrelationId [{CorrelationId}]",
                    message.Exchange, message.RoutingKey, message.CorrelationId);

                model.Channel.BasicPublish(message.Exchange, message.RoutingKey, isMandatory, properties, message.Content);

                if (enablePublisherConfirms)
                {
                    if (publisherConfirmsTimeout > TimeSpan.Zero)
                    {
                        model.Channel.WaitForConfirms(publisherConfirmsTimeout.Value);
                    }
                    else
                    {
                        model.Channel.WaitForConfirms();
                    }
                }
            }
            finally
            {
                _channels.Return(model);
            }
        }
    }
}
