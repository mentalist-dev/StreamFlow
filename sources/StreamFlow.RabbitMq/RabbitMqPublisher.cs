using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;
using StreamFlow.Pipes;
using StreamFlow.RabbitMq.Connection;

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

        public async Task<PublishResponse> PublishAsync<T>(T message, PublishOptions? options = null, CancellationToken cancellationToken = default) where T: class
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

            var request = new PublishRequest(options, isMandatory);

            await _pipe
                .ExecuteAsync(_services, context, GetPublishAction(request))
                .ConfigureAwait(false);

            return request.Response ?? new PublishResponse();
        }

        private Func<IMessageContext, Task> GetPublishAction(PublishRequest request)
        {
            return context =>
            {
                var options = request.Options;
                var publisherConfirmsEnabled = options?.PublisherConfirmsEnabled ?? false;
                var publisherConfirmsTimeout = options?.PublisherConfirmsTimeout;

                bool isMandatory = request.IsMandatory;

                request.Response = Publish(context, isMandatory, publisherConfirmsEnabled, publisherConfirmsTimeout);

                return Task.CompletedTask;
            };
        }

        private PublishResponse Publish(IMessageContext message, bool isMandatory, bool enablePublisherConfirms, TimeSpan? publisherConfirmsTimeout)
        {
            var model = _channels.Get();
            var subscribedEvents = false;
            var response = new PublishResponse();
            try
            {
                var properties = model.Channel.CreateBasicProperties();

                message.MapTo(properties);

                _logger.LogDebug(
                    "Publishing message to exchange [{ExchangeName}] using routing key [{RoutingKey}] with CorrelationId [{CorrelationId}]",
                    message.Exchange, message.RoutingKey, message.CorrelationId);

                if (model.Confirmation == ConfirmationType.PublisherConfirms)
                {
                    response.Sequence(model.Channel.NextPublishSeqNo);
                    if (enablePublisherConfirms)
                    {
                        subscribedEvents = true;
                        model.Channel.BasicAcks += OnAcknowledge;
                        model.Channel.BasicNacks += OnReject;
                    }
                }

                model.Channel.BasicPublish(message.Exchange, message.RoutingKey, isMandatory, properties, message.Content);

                if (enablePublisherConfirms)
                {
                    model.Confirm(publisherConfirmsTimeout);
                }
            }
            finally
            {
                if (subscribedEvents)
                {
                    model.Channel.BasicAcks -= OnAcknowledge;
                    model.Channel.BasicNacks -= OnReject;
                }

                _channels.Return(model);
            }

            void OnAcknowledge(object? sender, BasicAckEventArgs args)
            {
                var deliveryTag = args.DeliveryTag;
                var multiple = args.Multiple;
                response.Acknowledged(deliveryTag, multiple);
            }

            void OnReject(object? sender, BasicNackEventArgs args)
            {
                var deliveryTag = args.DeliveryTag;
                var multiple = args.Multiple;
                response.Rejected(deliveryTag, multiple);
            }

            return response;
        }

        private class PublishRequest
        {
            public PublishOptions? Options { get; set; }
            public bool IsMandatory { get; set; }

            public PublishResponse? Response { get; set; }

            public PublishRequest(PublishOptions? options, bool isMandatory)
            {
                Options = options;
                IsMandatory = isMandatory;
            }
        }
    }
}
