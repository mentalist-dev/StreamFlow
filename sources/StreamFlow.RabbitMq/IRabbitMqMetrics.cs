namespace StreamFlow.RabbitMq
{
    public interface IRabbitMqMetrics
    {
        IDisposable? Publishing(string exchangeName);
        void PublishingError(string exchangeName);

        IDisposable? Consuming(string exchangeName, string queue);
        void MessageConsumerError(string exchangeName, string queue);

        void BusPublishing();
        void BusPublishingError();
    }

    internal class NoRabbitMqMetrics : IRabbitMqMetrics
    {
        public IDisposable? Publishing(string exchangeName)
        {
            return null;
        }

        public void PublishingError(string exchangeName)
        {
        }

        public IDisposable? Consuming(string exchangeName, string queue)
        {
            return null;
        }

        public void MessageConsumerError(string exchangeName, string queue)
        {
        }

        public void BusPublishing()
        {
        }

        public void BusPublishingError()
        {
        }
    }
}
