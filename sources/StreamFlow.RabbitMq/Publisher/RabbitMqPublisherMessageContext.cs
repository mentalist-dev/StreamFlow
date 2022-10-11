namespace StreamFlow.RabbitMq.Publisher;

internal sealed class RabbitMqPublisherMessageContext : MessageContext
{
    public PublishOptions? PublishOptions { get; }

    public RabbitMqPublisherMessageContext(ReadOnlyMemory<byte> content, RabbitMqPublisherOptions globalOptions, PublishOptions? publishOptions) : base(content)
    {
        PublishOptions = publishOptions;

        DeclareExchange = publishOptions?.CreateExchangeEnabled ?? globalOptions.ExchangeDeclarationEnabled;
        IgnoreNoRoutes = publishOptions?.IgnoreNoRouteEvents ?? globalOptions.IgnoreNoRouteEventsEnabled;
    }

    public bool DeclareExchange { get; }
    public bool IgnoreNoRoutes { get; }
}
