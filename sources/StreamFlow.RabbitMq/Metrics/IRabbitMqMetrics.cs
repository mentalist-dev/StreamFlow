// ReSharper disable once CheckNamespace
namespace StreamFlow.RabbitMq;

public interface IRabbitMqMetrics
{
    IDurationMetric? Publishing(string exchangeName, bool publishedByBus);
    void PublishingEvent(string exchangeName, string eventName, TimeSpan duration);
    void PublishingError(string exchangeName, bool publishedByBus);

    IDurationMetric? Consuming(string exchangeName, string queue);
    void MessageConsumerError(string exchangeName, string queue);

    void PublishingByBus();
    void PublishingByBusError();

    void ReportPublisherPoolSize(int poolSize);
    void ReportPublisherPoolInUse(int publishersInUse);
}
