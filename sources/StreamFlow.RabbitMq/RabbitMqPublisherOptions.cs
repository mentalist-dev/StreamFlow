namespace StreamFlow.RabbitMq;

public class RabbitMqPublisherOptions
{
    internal bool ExchangeDeclarationEnabled { get; private set; }
    internal bool IgnoreNoRouteEventsEnabled { get; private set; }

    public RabbitMqPublisherOptions EnableExchangeDeclaration(bool enable = true)
    {
        ExchangeDeclarationEnabled = enable;
        return this;
    }

    public RabbitMqPublisherOptions IgnoreNoRouteEvents(bool enable = true)
    {
        IgnoreNoRouteEventsEnabled = enable;
        return this;
    }
}
