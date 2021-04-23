using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace StreamFlow.RabbitMq.Server
{
    public class RabbitMqHostedService: IHostedService
    {
        private readonly IRabbitMqServer _server;
        private readonly IConsumerRegistrations _registrations;

        public RabbitMqHostedService(IRabbitMqServer server, IConsumerRegistrations registrations)
        {
            _server = server;
            _registrations = registrations;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var consumer in _registrations.Consumers)
            {
                _server.Start(consumer);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _server.Stop();
            return Task.CompletedTask;
        }
    }
}
