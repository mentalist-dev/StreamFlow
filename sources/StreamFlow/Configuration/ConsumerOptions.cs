using System;

namespace StreamFlow.Configuration
{
    public class ConsumerOptions: IConsumerOptionsBuilder
    {
        public string? ConsumerGroup { get; private set; }
        public int ConsumerCount { get; private set; } = 1;
        public bool AutoAck { get; private set; } = true;
        public QueueOptions Queue { get; } = new();

        IConsumerOptionsBuilder IConsumerOptionsBuilder.ConsumerGroup(string consumerGroupName)
        {
            ConsumerGroup = consumerGroupName;
            return this;
        }

        IConsumerOptionsBuilder IConsumerOptionsBuilder.ConsumerCount(int consumerCount)
        {
            ConsumerCount = consumerCount;
            return this;
        }

        IConsumerOptionsBuilder IConsumerOptionsBuilder.AutoAcknowledge(bool autoAck)
        {
            AutoAck = autoAck;
            return this;
        }

        IConsumerOptionsBuilder IConsumerOptionsBuilder.ConfigureQueue(Action<QueueOptions> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            configure(Queue);

            return this;
        }
    }
}
