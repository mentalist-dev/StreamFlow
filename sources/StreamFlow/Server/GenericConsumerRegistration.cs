using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Configuration;

namespace StreamFlow.Server
{
    public class GenericConsumerRegistration<TConsumer> : IConsumerRegistration
        where TConsumer : class
    {
        public GenericConsumerRegistration(Type requestType, ConsumerOptions consumerOptions)
        {
            RequestType = requestType;
            Options = consumerOptions;
        }

        public ConsumerOptions Options { get; }

        public Type RequestType { get; }
        public Type ConsumerType => typeof(TConsumer);

        public async Task ExecuteAsync(IServiceProvider provider, IMessageContext context)
        {
            var formatter = provider.GetRequiredService<IMessageSerializer>();

            var methodInfo = formatter.GetType().GetMethod(nameof(formatter.Deserialize));
            var deserialize = methodInfo?.MakeGenericMethod(RequestType);

            // var messageContent = formatter.Deserialize<TRequest>(context.Content);
            var messageContent = deserialize?.Invoke(formatter, new object[] { context.Content });

            if (messageContent == null)
                throw new Exception("Unable to deserialize");

            Type messageType = typeof(Message<>);
            Type messageGenericType = messageType.MakeGenericType(messageContent.GetType());

            // var message = new Message<TRequest>(messageContent, context);
            var message = Activator.CreateInstance(messageGenericType, messageContent, context);
            if (message == null)
                throw new NullReferenceException($"Unable to create type {messageGenericType}");

            var consumer = provider.GetRequiredService<TConsumer>();

            var handleMethodInfo = consumer.GetType().GetMethod(nameof(IConsumer<Type>.Handle));
            // var handle = handleMethodInfo?.MakeGenericMethod(consumer.GetType());

            var task = handleMethodInfo?.Invoke(consumer, new [] {message}) as Task;
            if (task == null)
                throw new NullReferenceException($"Unable to invoke consumer handle method");

            await task;
        }
    }
}
