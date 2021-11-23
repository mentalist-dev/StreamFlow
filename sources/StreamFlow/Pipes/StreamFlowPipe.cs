namespace StreamFlow.Pipes
{
    public interface IStreamFlowConsumerPipe
    {
        Task ExecuteAsync(IServiceProvider provider, IMessageContext context, Func<IMessageContext, Task> finalAction);
    }

    public interface IStreamFlowPublisherPipe
    {
        Task ExecuteAsync(IServiceProvider provider, IMessageContext context, Func<IMessageContext, Task> finalAction);
    }

    public class StreamFlowPipe: IStreamFlowConsumerPipe, IStreamFlowPublisherPipe
    {
        private readonly List<Func<IServiceProvider, IMessageContext, Func<IMessageContext, Task>, Task>> _actions = new();

        Task IStreamFlowConsumerPipe.ExecuteAsync(IServiceProvider provider, IMessageContext context, Func<IMessageContext, Task> finalAction)
        {
            return ExecuteAsync(provider, context, finalAction);
        }

        Task IStreamFlowPublisherPipe.ExecuteAsync(IServiceProvider provider, IMessageContext context, Func<IMessageContext, Task> finalAction)
        {
            return ExecuteAsync(provider, context, finalAction);
        }

        public void AddRange(IEnumerable<Func<IServiceProvider, IMessageContext, Func<IMessageContext, Task>, Task>> actions)
        {
            _actions.AddRange(actions);
        }

        private Task ExecuteAsync(IServiceProvider provider, IMessageContext context, Func<IMessageContext, Task> finalAction)
        {
            var action = finalAction;

            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                var middleware = _actions[i];
                action = Build(provider, middleware, action);
            }

            return action(context);
        }

        private Func<IMessageContext, Task> Build(IServiceProvider provider
            , Func<IServiceProvider, IMessageContext, Func<IMessageContext, Task>, Task> middleware
            , Func<IMessageContext, Task> next)
        {
            return context => middleware(provider, context, next);
        }
    }
}
