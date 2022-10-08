using Microsoft.Extensions.Logging;
using StreamFlow.Configuration;

namespace StreamFlow.RabbitMq.Server;

public interface IRabbitMqServerController
{
    Task StartAsync(TimeSpan timeout, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public class RabbitMqServerController: IRabbitMqServerController
{
    private readonly IRabbitMqServer _server;
    private readonly IConsumerRegistrations _registrations;
    private readonly ILogger<RabbitMqServerController> _logger;

    public RabbitMqServerController(IRabbitMqServer server, IConsumerRegistrations registrations, ILogger<RabbitMqServerController> logger)
    {
        _server = server;
        _registrations = registrations;
        _logger = logger;
    }

    public async Task StartAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        foreach (var consumer in _registrations.Consumers)
        {
            try
            {
                await _server
                    .Start(consumer, timeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to start consumer {RequestType} for consumer {ConsumerType}", consumer.RequestType, consumer.ConsumerType);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server.Stop();
        return Task.CompletedTask;
    }
}
