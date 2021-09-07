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
        private readonly StreamFlowOptions _options;

        public RabbitMqConventions(StreamFlowOptions options)
        {
            _options = options;
        }

        public string GetExchangeName(Type requestType)
        {
            return requestType.Name;
        }

        public string GetQueueName(Type requestType, Type consumerType, string? consumerGroup)
        {
            var queueName = CreateQueueNameBase(requestType, consumerType);
            queueName = AddServiceId(queueName, _options.ServiceId);
            queueName = AddConsumerGroup(queueName, consumerGroup);
            return queueName;
        }

        public string GetErrorQueueName(Type requestType, Type consumerType, string? consumerGroup)
        {
            var queueName = CreateQueueNameBase(requestType, consumerType);
            queueName = AddServiceId(queueName, _options.ServiceId);
            queueName = AddConsumerGroup(queueName, consumerGroup);
            return AddErrorSuffix(queueName);
        }

        protected virtual string CreateQueueNameBase(Type requestType, Type consumerType)
        {
            return $"{requestType.Name}:{consumerType.Name}";
        }

        protected virtual string AddServiceId(string queueName, string? serviceId)
        {
            if (!string.IsNullOrWhiteSpace(serviceId))
            {
                queueName += ":" + serviceId;
            }

            return queueName;
        }

        protected virtual string AddConsumerGroup(string queueName, string? consumerGroup)
        {
            if (!string.IsNullOrWhiteSpace(consumerGroup))
            {
                queueName += ":" + consumerGroup;
            }

            return queueName;
        }

        protected virtual string AddErrorSuffix(string queueName)
        {
            return $"{queueName}:Error";
        }
    }
}
