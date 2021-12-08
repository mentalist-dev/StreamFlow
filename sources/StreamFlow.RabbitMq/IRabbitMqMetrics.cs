namespace StreamFlow.RabbitMq
{
    public interface IRabbitMqMetrics
    {
        IDurationMetric? Publishing(string exchangeName);
        void PublishingEvent(string exchangeName, string eventName, TimeSpan duration);
        void PublishingError(string exchangeName);

        IDurationMetric? Consuming(string exchangeName, string queue);
        void MessageConsumerError(string exchangeName, string queue);

        void BusPublishing();
        void BusPublishingError();
    }

    internal class NoRabbitMqMetrics : IRabbitMqMetrics
    {
        public IDurationMetric? Publishing(string exchangeName)
        {
            return null;
        }

        public void PublishingEvent(string exchangeName, string eventName, TimeSpan duration)
        {
        }

        public void PublishingError(string exchangeName)
        {
        }

        public IDurationMetric? Consuming(string exchangeName, string queue)
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

    public interface IDurationMetric : IDisposable
    {
        void Complete();
    }
}
