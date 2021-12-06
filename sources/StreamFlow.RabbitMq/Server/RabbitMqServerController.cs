using StreamFlow.Configuration;

namespace StreamFlow.RabbitMq.Server
{
    public interface IRabbitMqServerController
    {
        Task StartAsync(TimeSpan timeout, CancellationToken cancellationToken);
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

        public async Task StartAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            foreach (var consumer in _registrations.Consumers)
            {
                await _server
                    .Start(consumer, timeout, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _server.Stop();
            return Task.CompletedTask;
        }
    }
}
