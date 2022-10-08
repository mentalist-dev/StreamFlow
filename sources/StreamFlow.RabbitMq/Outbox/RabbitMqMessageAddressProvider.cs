using StreamFlow.Outbox;

namespace StreamFlow.RabbitMq.Outbox;

internal class RabbitMqMessageAddressProvider: IOutboxMessageAddressProvider
{
    private readonly IRabbitMqConventions _conventions;

    public RabbitMqMessageAddressProvider(IRabbitMqConventions conventions)
    {
        _conventions = conventions;
    }

    public string Get<T>(T message, PublishOptions? options = null) where T : class
    {
        return _conventions.GetExchangeName(message.GetType());
    }
}
