using System;

namespace StreamFlow.RabbitMq
{
    public interface IRabbitMqConventions
    {
        string GetExchangeName(Type requestType);
        string GetQueueName(Type consumerType, string? consumerGroup);
        string GetErrorQueueName(Type consumerType, string? consumerGroup);
    }

    public class RabbitMqConventions: IRabbitMqConventions
    {
        public string GetExchangeName(Type requestType)
        {
            return requestType.Name;
        }

        public string GetQueueName(Type consumerType, string? consumerGroup)
        {
            var queueName = consumerType.Name;

            if (!string.IsNullOrWhiteSpace(consumerGroup))
            {
                queueName += ":" + consumerGroup;
            }

            return queueName;
        }

        public string GetErrorQueueName(Type consumerType, string? consumerGroup)
        {
            var queueName = consumerType.Name;

            if (!string.IsNullOrWhiteSpace(consumerGroup))
            {
                queueName += ":" + consumerGroup;
            }

            return $"{queueName}:Error";
        }
    }
}
