namespace StreamFlow
{
    public interface IMessage<out TRequest>
    {
        TRequest Body { get; }
    }

    public class Message<TRequest> : IMessage<TRequest>
    {
        public Message(TRequest body)
        {
            Body = body;
        }

        public TRequest Body { get; }
    }
}