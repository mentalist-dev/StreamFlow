using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client.Events;
using StreamFlow.Configuration;

namespace StreamFlow.RabbitMq.Server
{
    public interface ILoggerScopeStateFactory
    {
        List<KeyValuePair<string, object>>? Create(BasicDeliverEventArgs @event, ConsumerOptions consumerOptions, RabbitMqConsumerInfo consumerInfo, string correlationId);
    }

    public class LoggerScopeStateFactory: ILoggerScopeStateFactory
    {
        public virtual List<KeyValuePair<string, object>> Create(BasicDeliverEventArgs @event, ConsumerOptions consumerOptions, RabbitMqConsumerInfo consumerInfo, string correlationId)
        {
            var state = new List<KeyValuePair<string, object>>
            {
                new("CorrelationId", correlationId),
                new("Exchange", consumerInfo.Exchange),
                new("Queue", consumerInfo.Queue),
                new("RoutingKey", consumerInfo.RoutingKey)
            };

            if (consumerOptions.IncludeHeadersToLoggerScope && @event.BasicProperties?.Headers != null)
            {
                foreach (var header in @event.BasicProperties.Headers)
                {
                    var key = header.Key;
                    var value = header.Value;

                    if (value == null)
                        continue;

                    if (!consumerOptions.ExcludeHeaderNamesFromLoggerScope.Contains(key))
                    {
                        var convertedValue = ConvertValue(value);
                        if (convertedValue != null)
                        {
                            state.Add(new KeyValuePair<string, object>(key, convertedValue));
                        }
                    }
                }
            }

            return state;
        }

        protected virtual object? ConvertValue(object value)
        {
            var convertedValue = value;

            if (value.GetType() == typeof(byte[]))
            {
                convertedValue = Encoding.UTF8.GetString((byte[])value);
            }

            return convertedValue;
        }
    }
}
