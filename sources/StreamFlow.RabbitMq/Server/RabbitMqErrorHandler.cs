using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StreamFlow.Server;

namespace StreamFlow.RabbitMq.Server
{
    public interface IRabbitMqErrorHandler
    {
        Task HandleAsync(IModel channel, Exception exception, IConsumerRegistration consumerRegistration, BasicDeliverEventArgs @event, string queue);
    }

    public class RabbitMqErrorHandler: IRabbitMqErrorHandler
    {
        private readonly IRabbitMqConventions _conventions;
        private readonly ILogger<RabbitMqErrorHandler> _logger;

        public RabbitMqErrorHandler(IRabbitMqConventions conventions, ILogger<RabbitMqErrorHandler> logger)
        {
            _conventions = conventions;
            _logger = logger;
        }

        public Task HandleAsync(IModel channel, Exception exception, IConsumerRegistration consumerRegistration, BasicDeliverEventArgs @event, string queue)
        {
            var errorQueueName = _conventions.GetErrorQueueName(
                consumerRegistration.RequestType,
                consumerRegistration.ConsumerType,
                consumerRegistration.Options.ConsumerGroup
            );

            _logger.LogDebug("Moving message to error queue [{ErrorQueueName}].", errorQueueName);

            try
            {
                var properties = @event.BasicProperties;

                properties.Headers ??= new Dictionary<string, object>();
                properties.Headers["SF:OriginalExchange"] = @event.Exchange;
                properties.Headers["SF:OriginalQueue"] = queue;
                properties.Headers["SF:OriginalRoutingKey"] = @event.RoutingKey;
                properties.Headers["SF:ExceptionTime"] = DateTime.UtcNow.ToString("O");
                properties.Headers["SF:ExceptionMessage"] = exception.Message;
                if (!string.IsNullOrWhiteSpace(exception.StackTrace))
                {
                    properties.Headers["SF:ExceptionStackTrace"] = exception.StackTrace;
                }

                channel.QueueDeclare(errorQueueName, true, false, false);
                channel.BasicPublish(string.Empty, errorQueueName, true, properties, @event.Body);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to send message to error queue [{ErrorQueue}].", errorQueueName);
                throw;
            }

            return Task.CompletedTask;
        }
    }
}
