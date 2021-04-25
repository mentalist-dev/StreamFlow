using System;

namespace StreamFlow.RabbitMq
{
    public interface IRabbitMqConventions
    {
        string GetExchangeName(Type requestType);
        string GetQueueName(Type requestType, Type consumerType, string? consumerGroup);
        string GetErrorQueueName(Type requestType, Type consumerType, string? consumerGroup);
    }

    public class RabbitMqConventions: IRabbitMqConventions
    {
        public string GetExchangeName(Type requestType)
        {
            return requestType.Name;
        }

        public string GetQueueName(Type requestType, Type consumerType, string? consumerGroup)
        {
            var queueName = $"{requestType.Name}:{consumerType.Name}";

            if (!string.IsNullOrWhiteSpace(consumerGroup))
            {
                queueName += ":" + consumerGroup;
            }

            return queueName;
        }

        public string GetErrorQueueName(Type requestType, Type consumerType, string? consumerGroup)
        {
            var queueName = $"{requestType.Name}:{consumerType.Name}";

            if (!string.IsNullOrWhiteSpace(consumerGroup))
            {
                queueName += ":" + consumerGroup;
            }

            return $"{queueName}:Error";
        }
    }
}
