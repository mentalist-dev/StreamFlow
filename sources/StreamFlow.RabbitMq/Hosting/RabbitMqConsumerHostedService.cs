using Microsoft.Extensions.Hosting;
using StreamFlow.RabbitMq.Server;

namespace StreamFlow.RabbitMq.Hosting;

public class RabbitMqConsumerHostedService: IHostedService
{
    private readonly IRabbitMqServerController _controller;

    public RabbitMqConsumerHostedService(IRabbitMqServerController controller)
    {
        _controller = controller;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _controller.StartAsync(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _controller.StopAsync(cancellationToken);
    }
}
