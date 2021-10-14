using System;
using System.Linq;

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
            return $"{_options.ExchangePrefix}{requestType.Name}";
        }

        public string GetQueueName(Type requestType, Type consumerType, string? consumerGroup)
        {
            var queueName = CreateQueueNameBase(requestType, consumerType);
            queueName = AddServiceId(queueName, _options.ServiceId);
            queueName = AddConsumerGroup(queueName, consumerGroup);
            return $"{_options.ExchangePrefix}{queueName}";
        }

        public string GetErrorQueueName(Type requestType, Type consumerType, string? consumerGroup)
        {
            var queueName = CreateQueueNameBase(requestType, consumerType);
            queueName = AddServiceId(queueName, _options.ServiceId);
            queueName = AddConsumerGroup(queueName, consumerGroup);
            var errorQueueName = AddErrorSuffix(queueName);
            return $"{_options.ExchangePrefix}{errorQueueName}";
        }

        protected virtual string CreateQueueNameBase(Type requestType, Type consumerType)
        {
            var consumerTypeName = GetTypeName(consumerType);

            var separator = GetSeparator();
            return $"{requestType.Name}{separator}{consumerTypeName}";
        }

        protected virtual string GetTypeName(Type consumerType)
        {
            var consumerTypeName = consumerType.Name;
            if (consumerType.IsGenericType)
            {
                var index = consumerTypeName.IndexOf('`');
                if (index > 0)
                {
                    consumerTypeName = consumerTypeName.Substring(0, index);

                    var arguments = consumerType.GenericTypeArguments.Select(GetTypeName);
                    var joined = string.Join(',', arguments);

                    if (!string.IsNullOrWhiteSpace(joined))
                    {
                        consumerTypeName += $"<{joined}>";
                    }
                }
            }

            return consumerTypeName;
        }

        protected virtual string AddServiceId(string queueName, string? serviceId)
        {
            if (!string.IsNullOrWhiteSpace(serviceId))
            {
                var separator = GetSeparator();

                queueName += separator + serviceId;
            }

            return queueName;
        }

        protected virtual string AddConsumerGroup(string queueName, string? consumerGroup)
        {
            if (!string.IsNullOrWhiteSpace(consumerGroup))
            {
                var separator = GetSeparator();

                queueName += separator + consumerGroup;
            }

            return queueName;
        }

        protected virtual string AddErrorSuffix(string queueName)
        {
            var suffix = _options.ErrorSuffix;

            if (suffix == null)
            {
                var separator = GetSeparator();

                suffix = separator + "Error";
            }

            return $"{queueName}{suffix}";
        }

        private string GetSeparator()
        {
            var separator = _options.Separator;
            if (string.IsNullOrWhiteSpace(separator))
                separator = ":";
            return separator;
        }
    }
}
