using System.Threading.Tasks;

namespace StreamFlow
{
    public interface IConsumer<TRequest>
    {
        Task Handle(IMessage<TRequest> message);
    }
}