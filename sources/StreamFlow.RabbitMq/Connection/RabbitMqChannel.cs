using System;
using RabbitMQ.Client;

namespace StreamFlow.RabbitMq.Connection
{
    public class RabbitMqChannel: IDisposable
    {
        public RabbitMqChannel(IConnection connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Channel = connection.CreateModel();
            Channel.ConfirmSelect();
        }

        public IConnection Connection { get; }
        public IModel Channel { get; }

        public void Dispose()
        {
            Channel.Dispose();
        }
    }
}
