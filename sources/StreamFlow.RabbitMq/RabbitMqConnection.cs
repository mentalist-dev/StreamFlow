using RabbitMQ.Client;

namespace StreamFlow.RabbitMq
{
    public interface IRabbitMqConnection
    {
        IConnection Create();
    }

    public class RabbitMqConnection: IRabbitMqConnection
    {
        private readonly ConnectionFactory _connectionFactory;

        public RabbitMqConnection(string hostName, string userName, string password, string virtualHost)
        {
            _connectionFactory = new ConnectionFactory
            {
                HostName = hostName,
                UserName = userName,
                Password = password,
                VirtualHost = virtualHost,
                DispatchConsumersAsync = true
            };
        }

        public IConnection Create()
        {
            return _connectionFactory.CreateConnection();
        }
    }
}
