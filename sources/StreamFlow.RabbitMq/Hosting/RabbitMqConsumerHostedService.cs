using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamFlow.RabbitMq.Server;

namespace StreamFlow.RabbitMq.Hosting;

public class RabbitMqConsumerHostedService: IHostedService
{
    private readonly IRabbitMqServerController _controller;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<RabbitMqConsumer> _logger;

    public RabbitMqConsumerHostedService(IRabbitMqServerController controller, IHostApplicationLifetime lifetime, ILogger<RabbitMqConsumer> logger)
    {
        _controller = controller;
        _lifetime = lifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Starting RabbitMqConsumerHostedService");

        Task.Factory.StartNew(
            () => _controller.StartAsync(Timeout.InfiniteTimeSpan, _lifetime.ApplicationStopping),
            cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Stopping RabbitMqConsumerHostedService");

        return _controller.StopAsync(cancellationToken);
    }
}
