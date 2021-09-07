using System;

namespace StreamFlow.RabbitMq
{
    public interface IRabbitMqMetrics
    {
        void MessagePublished(string exchangeName);
        IDisposable? ConsumerInProgress(string exchangeName, string queue);
        void MessageConsumerError(string exchangeName, string queue);
    }

    internal class NoRabbitMqMetrics : IRabbitMqMetrics
    {
        public void MessagePublished(string exchangeName)
        {
        }

        public IDisposable? ConsumerInProgress(string exchangeName, string queue)
        {
            return null;
        }

        public void MessageConsumerError(string exchangeName, string queue)
        {
        }
    }
}
