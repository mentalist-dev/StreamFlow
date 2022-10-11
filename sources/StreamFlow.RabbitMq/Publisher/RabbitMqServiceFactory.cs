using Microsoft.Extensions.Logging;

namespace StreamFlow.RabbitMq.Publisher;

internal interface IRabbitMqServiceFactory
{
    IRabbitMqService Create();
}

internal class RabbitMqServiceFactory: IRabbitMqServiceFactory
{
    private readonly IRabbitMqPublisherConnection _connection;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqServiceFactory(IRabbitMqPublisherConnection connection, ILogger<RabbitMqPublisher> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public IRabbitMqService Create()
    {
        return new RabbitMqService(_connection, _logger);
    }
}
