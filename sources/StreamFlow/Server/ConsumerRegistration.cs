using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Configuration;

namespace StreamFlow.Server;

public interface IConsumerRegistration
{
    ConsumerOptions Options { get; }
    StreamFlowDefaults? Default { get; }

    Type RequestType { get; }
    Type ConsumerType { get; }

    Task ExecuteAsync(IServiceProvider provider, IMessageContext context, CancellationToken cancellationToken);
}

public class ConsumerRegistration<TRequest, TConsumer> : IConsumerRegistration
    where TConsumer : class, IConsumer<TRequest>
{
    public ConsumerRegistration(ConsumerOptions consumerOptions, StreamFlowDefaults? @default)
    {
        Options = consumerOptions;
        Default = @default;
    }

    public ConsumerOptions Options { get; }
    public StreamFlowDefaults? Default { get; }

    public Type RequestType => typeof(TRequest);
    public Type ConsumerType => typeof(TConsumer);

    public async Task ExecuteAsync(IServiceProvider provider, IMessageContext context, CancellationToken cancellationToken)
    {
        var formatter = provider.GetRequiredService<IMessageSerializer>();

        var messageContent = formatter.Deserialize<TRequest>(context.Content);
        if (messageContent == null)
            throw new Exception("Unable to deserialize");

        var message = new Message<TRequest>(messageContent, context);

        var consumer = provider.GetRequiredService<TConsumer>();
        await consumer.Handle(message, cancellationToken).ConfigureAwait(false);
    }
}
