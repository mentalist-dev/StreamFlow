using System.Threading.Tasks;

namespace StreamFlow
{
    public interface IConsumer<in TRequest>
    {
        Task Handle(IMessage<TRequest> message);
    }

    public interface IMessage<out TRequest>
    {
        TRequest Body { get; }
        IMessageContext Context { get; }
    }

    public class Message<TRequest> : IMessage<TRequest>
    {
        public Message(TRequest body, IMessageContext context)
        {
            Body = body;
            Context = context;
        }

        public TRequest Body { get; }
        public IMessageContext Context { get; }
    }
}
