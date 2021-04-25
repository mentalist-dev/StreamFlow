namespace StreamFlow.Configuration
{
    public interface IQueueOptionsBuilder
    {
        IQueueOptionsBuilder Durable(bool durable = true);
        IQueueOptionsBuilder Exclusive(bool exclusive = true);
        IQueueOptionsBuilder AutoDelete(bool autoDelete = true);
        IQueueOptionsBuilder Argument(string key, object value);
    }
}
