using System;

namespace StreamFlow.RabbitMq
{
    public interface IRabbitMqMetrics
    {
        IDisposable? MessageInPublishing(string exchangeName);
        IDisposable? ConsumerInProgress(string exchangeName, string queue);
        void MessageConsumerError(string exchangeName, string queue);
        void PublisherChannelPoolSize(int poolSize);
    }

    internal class NoRabbitMqMetrics : IRabbitMqMetrics
    {
        public IDisposable? MessageInPublishing(string exchangeName)
        {
            return null;
        }

        public IDisposable? ConsumerInProgress(string exchangeName, string queue)
        {
            return null;
        }

        public void MessageConsumerError(string exchangeName, string queue)
        {
        }

        public void PublisherChannelPoolSize(int poolSize)
        {

        }
    }
}
