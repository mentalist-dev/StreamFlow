using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace StreamFlow
{
    public interface IRegistration
    {
        ConsumerOptions Options { get; }

        Type RequestType { get; }
        Type ConsumerType { get; }

        Task Execute(IServiceProvider provider, IExecutionContext context);
    }

    public class Registration<TRequest, TConsumer> : IRegistration
        where TConsumer : class, IConsumer<TRequest>
    {
        public Registration(ConsumerOptions consumerOptions)
        {
            Options = consumerOptions;
        }

        public ConsumerOptions Options { get; }

        public Type RequestType => typeof(TRequest);
        public Type ConsumerType => typeof(TConsumer);

        public async Task Execute(IServiceProvider provider, IExecutionContext context)
        {
            var formatter = provider.GetRequiredService<IMessageSerializer>();
            
            var messageContent = formatter.Deserialize<TRequest>(context.Content);
            if (messageContent == null)
                throw new Exception("Unable to deserialize");

            var message = new Message<TRequest>(messageContent);

            var consumer = provider.GetRequiredService<TConsumer>();
            await consumer.Handle(message);
        }
    }

    public interface IExecutionContext
    {
        public ReadOnlyMemory<byte> Content { get; }
        public IDictionary<string, object> Headers { get; }
        public string? CorrelationId { get; }
        public string? RoutingKey { get; }
    }
}