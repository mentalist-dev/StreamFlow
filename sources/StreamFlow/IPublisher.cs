using System.Threading.Tasks;

namespace StreamFlow
{
    public interface IPublisher
    {
        void Publish<T>(T message, PublishOptions? options = null) where T : class;
        Task PublishAsync<T>(T message, PublishOptions? options = null) where T : class;
    }
}