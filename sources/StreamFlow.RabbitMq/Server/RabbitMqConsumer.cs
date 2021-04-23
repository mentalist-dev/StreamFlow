using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace StreamFlow.RabbitMq.Server
{
    public interface IRabbitMqConsumer
    {
        string? ConsumerTag { get; }
    }

    public class RabbitMqConsumerInfo
    {
        public RabbitMqConsumerInfo(string exchange, string queue, string routingKey, bool autoAck)
        {
            Exchange = exchange;
            Queue = queue;
            RoutingKey = routingKey;
            AutoAck = autoAck;
        }

        public string Exchange { get; init; }
        public string Queue { get; init; }
        public string RoutingKey { get; init; }
        public bool AutoAck { get; init; }
    }

    public class RabbitMqConsumer: IRabbitMqConsumer, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly IModel _channel;
        private readonly RabbitMqConsumerInfo _info;
        private readonly ILogger<RabbitMqConsumer> _logger;
        private readonly AsyncEventingBasicConsumer _consumer;

        public RabbitMqConsumer(IServiceProvider services, IModel channel, RabbitMqConsumerInfo consumerInfo, ILogger<RabbitMqConsumer> logger)
        {
            _services = services;
            _channel = channel;
            _logger = logger;
            _info = consumerInfo;
            _consumer = new AsyncEventingBasicConsumer(channel);
        }

        public string? ConsumerTag { get; private set; }

        public void Start(IRegistration registration)
        {
            _consumer.Received += (_, @event) => OnReceived(@event, registration);
            ConsumerTag = _channel.BasicConsume(_info.Queue, _info.AutoAck, _consumer);
        }

        private async Task OnReceived(BasicDeliverEventArgs @event, IRegistration registration)
        {
            var correlationId = @event.BasicProperties?.CorrelationId ?? string.Empty;
            _logger.LogDebug("Received message. Message info = {@MessageInfo}. CorrelationId = {CorrelationId}.", _info, correlationId);

            using var scope = _services.CreateScope();
            var serviceProvider = scope.ServiceProvider;

            try
            {
                var context = new RabbitMqExecutionContext(@event);

                var scopeFactory = serviceProvider.GetRequiredService<IRabbitMqScopeFactory>();
                using var rabbitMqScope = scopeFactory.Create(context);

                await registration.Execute(serviceProvider, context);

                if (!_info.AutoAck)
                {
                    _channel.BasicAck(@event.DeliveryTag, false);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Message handling failed. Message info = {@MessageInfo}.", _info);

                var errorHandler = CreateErrorHandler(serviceProvider, @event.DeliveryTag);
                await errorHandler.Handle(_channel, e, registration, @event, _info.Queue);

                if (!_info.AutoAck)
                {
                    _channel.BasicAck(@event.DeliveryTag, false);
                }
                else
                {
                    _channel.BasicNack(@event.DeliveryTag, false, true);
                }
            }
        }

        private IRabbitMqErrorHandler CreateErrorHandler(IServiceProvider serviceProvider, ulong deliveryTag)
        {
            try
            {
                var errorHandler = serviceProvider.GetRequiredService<IRabbitMqErrorHandler>();
                return errorHandler;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to create error handler <IRabbitMqErrorHandler>. Cancelling subscription. Shutting down consumer.");

                if (!string.IsNullOrWhiteSpace(ConsumerTag))
                {
                    _channel.BasicNack(deliveryTag, false, true);
                    _channel.BasicCancel(ConsumerTag);
                }

                throw;
            }
        }

        public void Dispose()
        {
            _logger.LogDebug("Disposing consumer. Message info = {@MessageInfo}.", _info);

            if (!string.IsNullOrWhiteSpace(ConsumerTag))
            {
                _consumer.HandleBasicCancel(ConsumerTag);
            }
            
            _channel.Dispose();
        }
    }
}