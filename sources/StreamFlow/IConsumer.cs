using System.Threading.Tasks;

namespace StreamFlow
{
    public interface IConsumer<in TRequest>
    {
        Task Handle(IConsumerMessage<TRequest> message);
    }

    public interface IConsumerMessage<out TRequest>
    {
        TRequest Body { get; }
    }

    public class ConsumerMessage<TRequest> : IConsumerMessage<TRequest>
    {
        public ConsumerMessage(TRequest body)
        {
            Body = body;
        }

        public TRequest Body { get; }
    }
}
