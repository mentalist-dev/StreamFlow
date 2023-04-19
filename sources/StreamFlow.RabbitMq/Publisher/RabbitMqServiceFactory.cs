using Microsoft.Extensions.Logging;

namespace StreamFlow.RabbitMq.Publisher;

internal interface IRabbitMqServiceFactory
{
    IRabbitMqService Create();
}

internal sealed class RabbitMqServiceFactory: IRabbitMqServiceFactory
{
    private readonly IRabbitMqPublisherConnection _connection;
    private readonly IRabbitMqMetrics _metrics;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqServiceFactory(IRabbitMqPublisherConnection connection, IRabbitMqMetrics metrics, ILogger<RabbitMqPublisher> logger)
    {
        _connection = connection;
        _metrics = metrics;
        _logger = logger;
    }

    public IRabbitMqService Create()
    {
        return new RabbitMqService(_connection, _metrics, _logger);
    }
}
