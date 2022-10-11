using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace StreamFlow.Pipes;

public interface IStreamFlowPipeBuilder
{
    void Use(Func<IMessageContext, Func<IMessageContext, Task>, Task> middleware);
    void Use<T>() where T : class, IStreamFlowMiddleware;
    void Use<T>(Func<IServiceProvider, T> factory) where T : class, IStreamFlowMiddleware;
}

public class StreamFlowPipeBuilder: IStreamFlowPipeBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Func<IServiceProvider, IMessageContext, Func<IMessageContext, Task>, Task>> _actions = new();

    public StreamFlowPipeBuilder(IServiceCollection services)
    {
        _services = services;
    }

    void IStreamFlowPipeBuilder.Use(Func<IMessageContext, Func<IMessageContext, Task>, Task> middleware)
    {
        _actions.Add((_, context, next) => middleware(context, next));
    }

    void IStreamFlowPipeBuilder.Use<T>()
    {
        _services.TryAddTransient<T>();

        _actions.Add((provider, context, next) =>
        {
            var mid = provider.GetRequiredService<T>();
            return mid.Invoke(context, next);
        });
    }

    public void Use<T>(Func<IServiceProvider, T> factory) where T : class, IStreamFlowMiddleware
    {
        _services.TryAddTransient(factory);

        _actions.Add((provider, context, next) =>
        {
            var mid = factory(provider);
            return mid.Invoke(context, next);
        });
    }

    public IEnumerable<Func<IServiceProvider, IMessageContext, Func<IMessageContext, Task>, Task>> Actions => _actions;
}
