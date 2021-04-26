using System;

namespace StreamFlow.Configuration
{
    public interface IConsumerOptionsBuilder
    {
        IConsumerOptionsBuilder ConsumerGroup(string consumerGroupName);
        IConsumerOptionsBuilder ConsumerCount(int consumerCount);
        IConsumerOptionsBuilder ConfigureQueue(Action<QueueOptions> configure);
    }
}
