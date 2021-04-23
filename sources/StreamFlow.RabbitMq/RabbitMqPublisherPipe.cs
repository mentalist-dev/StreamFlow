using System.Threading.Tasks;
using RabbitMQ.Client;

namespace StreamFlow.RabbitMq
{
    public interface IRabbitMqPublisherPipe
    {
        Task Execute<T>(IBasicProperties properties);
    }

    public class RabbitMqPublisherPipe: IRabbitMqPublisherPipe
    {
        public Task Execute<T>(IBasicProperties properties)
        {
            return Task.CompletedTask;
        }
    }
}
