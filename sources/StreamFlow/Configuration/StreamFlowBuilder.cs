using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StreamFlow.Pipes;
using StreamFlow.Server;

namespace StreamFlow.Configuration;

public interface IStreamFlowTransport
{
    IServiceCollection Services { get; }
    StreamFlowOptions Options { get; }
    IStreamFlowTransport Consumers(Action<IStreamFlowConsumer> build);
    IStreamFlowTransport ConfigureConsumerPipe(Action<IStreamFlowPipeBuilder> build);
    IStreamFlowTransport ConfigurePublisherPipe(Action<IStreamFlowPipeBuilder> build);
}

public interface IStreamFlowConsumer
{
    IStreamFlowConsumer Add<TRequest, TConsumer>(Action<IConsumerOptionsBuilder>? consumer = null)
        where TConsumer : class, IConsumer<TRequest>;

    IStreamFlowConsumer Add<TConsumer>(Action<IConsumerOptionsBuilder>? consumer = null)
        where TConsumer : class;
}

public class StreamFlowBuilder : IStreamFlowTransport, IStreamFlowConsumer
{
    private readonly ConsumerRegistrations _registrations;
    private readonly StreamFlowOptions _options;
    private readonly IServiceCollection _services;
    private readonly StreamFlowPipe _consumerPipe = new();
    private readonly StreamFlowPipe _publisherPipe = new();

    public StreamFlowBuilder(IServiceCollection services, ConsumerRegistrations registrations, StreamFlowOptions options)
    {
        _services = services;
        _registrations = registrations;
        _options = options;

        _services.AddSingleton<IStreamFlowConsumerPipe>(_consumerPipe);
        _services.AddSingleton<IStreamFlowPublisherPipe>(_publisherPipe);
    }

    IServiceCollection IStreamFlowTransport.Services => _services;
    StreamFlowOptions IStreamFlowTransport.Options => _options;

    IStreamFlowTransport IStreamFlowTransport.Consumers(Action<IStreamFlowConsumer> build)
    {
        build(this);
        return this;
    }

    IStreamFlowTransport IStreamFlowTransport.ConfigureConsumerPipe(Action<IStreamFlowPipeBuilder> build)
    {
        var pipe = new StreamFlowPipeBuilder(_services);
        build(pipe);

        _consumerPipe.AddRange(pipe.Actions);

        return this;
    }

    IStreamFlowTransport IStreamFlowTransport.ConfigurePublisherPipe(Action<IStreamFlowPipeBuilder> build)
    {
        var pipe = new StreamFlowPipeBuilder(_services);
        build(pipe);

        _publisherPipe.AddRange(pipe.Actions);

        return this;
    }

    IStreamFlowConsumer IStreamFlowConsumer.Add<TRequest, TConsumer>(Action<IConsumerOptionsBuilder>? consumer)
    {
        _services.TryAddTransient<TConsumer>();

        var consumerOptions = new ConsumerOptions();
        consumer?.Invoke(consumerOptions);

        var registration = new ConsumerRegistration<TRequest, TConsumer>(consumerOptions, _options.Default);

        _registrations.Add(registration);

        return this;
    }

    IStreamFlowConsumer IStreamFlowConsumer.Add<TConsumer>(Action<IConsumerOptionsBuilder>? consumer)
    {
        _services.TryAddTransient<TConsumer>();

        var interfaces = typeof(TConsumer).GetInterfaces();
        foreach (var @interface in interfaces)
        {
            if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IConsumer<>))
            {
                var consumerOptions = new ConsumerOptions();
                consumer?.Invoke(consumerOptions);

                var arguments = @interface.GetGenericArguments();

                var registration = new GenericConsumerRegistration<TConsumer>(arguments.First(), consumerOptions, _options.Default);

                _registrations.Add(registration);
            }
        }

        return this;
    }
}
