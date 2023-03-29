// ReSharper disable once CheckNamespace
namespace StreamFlow.RabbitMq;

internal class RabbitMqNoOpMetrics : IRabbitMqMetrics
{
    public IDurationMetric? PublicationCreated(string exchangeName)
    {
        return null;
    }

    public IDurationMetric? PublicationConsumed(string exchangeName)
    {
        return null;
    }

    public IDurationMetric? Published(string exchangeName)
    {
        return null;
    }

    public IDurationMetric? Consumed(string exchangeName, string queueName)
    {
        return null;
    }

    public void ConsumerError(string exchangeName, string queueName)
    {
        //
    }

    public void ConsumerCancelled(string exchangeName, string queueName)
    {
        //
    }

    public void ErrorQueuePublished(string originalExchangeName, string originalQueueName)
    {
        //
    }

    public void ErrorQueueFailed(string originalExchangeName, string originalQueueName)
    {
        //
    }

    public IDisposable? PublisherQueued(string exchangeName)
    {
        return null;
    }
}
