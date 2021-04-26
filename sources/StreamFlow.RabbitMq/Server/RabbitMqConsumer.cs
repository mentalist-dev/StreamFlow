using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StreamFlow.Pipes;
using StreamFlow.Server;

namespace StreamFlow.RabbitMq.Server
{
    public interface IRabbitMqConsumer
    {
        string? ConsumerTag { get; }
    }

    public class RabbitMqConsumerInfo
    {
        public RabbitMqConsumerInfo(string exchange, string queue, string routingKey)
        {
            Exchange = exchange;
            Queue = queue;
            RoutingKey = routingKey;
        }

        public string Exchange { get; init; }
        public string Queue { get; init; }
        public string RoutingKey { get; init; }
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
        public bool IsDisposed { get; private set; }

        public void Start(IConsumerRegistration consumerRegistration)
        {
            _consumer.Received += (_, @event) => OnReceivedAsync(@event, consumerRegistration);
            ConsumerTag = _channel.BasicConsume(_info.Queue, false, _consumer);
        }

        private async Task OnReceivedAsync(BasicDeliverEventArgs @event, IConsumerRegistration consumer)
        {
            if (IsDisposed)
                return;

            var correlationId = @event.BasicProperties?.CorrelationId ?? string.Empty;
            _logger.LogDebug("Received message. Message info = {@MessageInfo}. CorrelationId = {CorrelationId}.", _info, correlationId);

            using var scope = _services.CreateScope();
            var serviceProvider = scope.ServiceProvider;

            bool? acknowledge = false;
            try
            {
                var context = new RabbitMqConsumerMessageContext(@event);

                // find handler and consume message
                var executor = serviceProvider.GetRequiredService<IStreamFlowConsumerPipe>();
                await executor.ExecuteAsync(serviceProvider, context, ctx => consumer.ExecuteAsync(serviceProvider, ctx));

                acknowledge = true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Message handling failed. Message info = {@MessageInfo}.", _info);
                acknowledge = await HandleError(serviceProvider, @event, consumer, e);
            }
            finally
            {
                if (acknowledge != null)
                {
                    if (acknowledge == true)
                    {
                        _channel.BasicAck(@event.DeliveryTag, false);
                    }
                    else
                    {
                        ReturnMessageBackToQueue(@event.DeliveryTag);
                    }
                }
            }
        }

        private async Task<bool?> HandleError(IServiceProvider serviceProvider, BasicDeliverEventArgs @event, IConsumerRegistration consumerRegistration, Exception exception)
        {
            IRabbitMqErrorHandler? errorHandler;
            try
            {
                errorHandler = serviceProvider.GetRequiredService<IRabbitMqErrorHandler>();
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Unable to create error handler <IRabbitMqErrorHandler>. Cancelling subscription. Shutting down consumer.");

                try
                {
                    ReturnMessageBackToQueue(@event.DeliveryTag);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to send NACK to RabbitMQ server");
                }
                finally
                {
                    StopConsumer();
                }

                // response to RabbitMQ already handled
                return null;
            }

            try
            {
                await errorHandler.HandleAsync(_channel, exception, consumerRegistration, @event, _info.Queue);

                // error successfully handled, acknowledge current message
                return true;
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Message error handling failed. Message info = {@MessageInfo}.", _info);

                // error was not handled
                return false;
            }
        }

        private void ReturnMessageBackToQueue(ulong deliveryTag)
        {
            _channel.BasicNack(deliveryTag, false, true);
        }

        private void StopConsumer()
        {
            if (!string.IsNullOrWhiteSpace(ConsumerTag))
            {
                _logger.LogWarning("Cancelling RabbitMQ subscription");
                try
                {
                    _channel.BasicCancel(ConsumerTag);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to cancel RabbitMQ subscription");
                }
            }

            ConsumerTag = null;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            _logger.LogDebug("Disposing consumer. Message info = {@MessageInfo}.", _info);

            try
            {
                StopConsumer();

                if (_channel.IsOpen)
                {
                    _channel.Close();
                }

                _channel.Dispose();
            }
            finally
            {
                IsDisposed = true;
            }
        }
    }
}
