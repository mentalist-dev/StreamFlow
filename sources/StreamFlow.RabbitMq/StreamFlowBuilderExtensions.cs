using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StreamFlow.RabbitMq.Server;

namespace StreamFlow.RabbitMq
{
    public static class StreamFlowBuilderExtensions
    {
        public static IStreamFlow RabbitMqTransport(this IStreamFlow builder, Action<IStreamFlowRabbitMq> configure)
        {
            builder.Services.TryAddSingleton<IRabbitMqConventions, RabbitMqConventions>();
            builder.Services.TryAddSingleton<IMessageSerializer, RabbitMqMessageSerializer>();
            builder.Services.AddScoped<IPublisher, RabbitMqPublisher>();
            builder.Services.AddScoped<IRabbitMqPublisherPipe, RabbitMqPublisherPipe>();

            var rabbitMq = new StreamFlowRabbitMq(builder.Services);
            configure(rabbitMq);

            return builder;
        }
    }

    public interface IStreamFlowRabbitMq
    {
        IStreamFlowRabbitMq ConnectTo(string hostName, string userName, string password, string virtualHost = "/");
        IStreamFlowRabbitMq ConsumeInHostedService();
        IStreamFlowRabbitMq WithConsumerPipe<T>() where T : class, IRabbitMqConsumerPipe;
        IStreamFlowRabbitMq WithPublisherPipe<T>() where T : class, IRabbitMqPublisherPipe;
    }

    internal class StreamFlowRabbitMq: IStreamFlowRabbitMq
    {
        private readonly IServiceCollection _services;

        public StreamFlowRabbitMq(IServiceCollection services)
        {
            _services = services;
        }

        public IStreamFlowRabbitMq ConnectTo(string hostName, string userName, string password, string virtualHost = "/")
        {
            _services.AddSingleton<IRabbitMqConnection>(new RabbitMqConnection(hostName, userName, password, virtualHost));
            return this;
        }

        public IStreamFlowRabbitMq ConsumeInHostedService()
        {
            _services.AddSingleton<IRabbitMqServer, RabbitMqServer>();
            _services.AddTransient<IRabbitMqErrorHandler, RabbitMqErrorHandler>();
            _services.TryAddScoped<IRabbitMqConsumerPipe, RabbitMqConsumerPipe>();

            _services.AddHostedService<RabbitMqHostedService>();

            return this;
        }

        public IStreamFlowRabbitMq WithConsumerPipe<T>() where T: class, IRabbitMqConsumerPipe
        {
            _services.RemoveAll<IRabbitMqConsumerPipe>();
            _services.AddScoped<IRabbitMqConsumerPipe, T>();
            return this;
        }

        public IStreamFlowRabbitMq WithPublisherPipe<T>() where T: class, IRabbitMqPublisherPipe
        {
            _services.RemoveAll<IRabbitMqPublisherPipe>();
            _services.AddScoped<IRabbitMqPublisherPipe, T>();
            return this;
        }
    }
}
