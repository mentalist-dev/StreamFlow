using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace StreamFlow.RabbitMq.Server.Hosting
{
    public class RabbitMqHostedService: IHostedService
    {
        private readonly IRabbitMqServerController _controller;

        public RabbitMqHostedService(IRabbitMqServerController controller)
        {
            _controller = controller;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _controller.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _controller.StopAsync(cancellationToken);
        }
    }
}
