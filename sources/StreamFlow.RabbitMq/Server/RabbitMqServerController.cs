using System.Threading;
using System.Threading.Tasks;
using StreamFlow.Configuration;

namespace StreamFlow.RabbitMq.Server
{
    public interface IRabbitMqServerController
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }

    public class RabbitMqServerController: IRabbitMqServerController
    {
        private readonly IRabbitMqServer _server;
        private readonly IConsumerRegistrations _registrations;

        public RabbitMqServerController(IRabbitMqServer server, IConsumerRegistrations registrations)
        {
            _server = server;
            _registrations = registrations;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var consumer in _registrations.Consumers)
            {
                _server.Start(consumer, cancellationToken);
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
