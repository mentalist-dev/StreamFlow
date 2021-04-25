using System;

namespace StreamFlow.Configuration
{
    public interface IConsumerOptionsBuilder
    {
        IConsumerOptionsBuilder ConsumerGroup(string consumerGroupName);
        IConsumerOptionsBuilder ConsumerCount(int consumerCount);
        IConsumerOptionsBuilder AutoAcknowledge(bool autoAck = true);
        IConsumerOptionsBuilder ConfigureQueue(Action<QueueOptions> configure);
    }
}
