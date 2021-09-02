namespace StreamFlow.Outbox
{
    public interface IOutboxMessageAddressProvider
    {
        string Get<T>(T message, PublishOptions? options = null) where T : class;
    }

    public class OutboxMessageAddressProvider: IOutboxMessageAddressProvider
    {
        public string Get<T>(T message, PublishOptions? options = null) where T : class
        {
            return string.Empty;
        }
    }
}
