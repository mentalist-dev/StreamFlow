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

            var rabbitMq = new StreamFlowRabbitMq(builder.Services);
            configure(rabbitMq);

            return builder;
        }
    }

    public interface IStreamFlowRabbitMq
    {
        IStreamFlowRabbitMq ConnectTo(string hostName, string userName, string password, string virtualHost = "/");
        IStreamFlowRabbitMq ConsumeInHostedService();
        IStreamFlowRabbitMq WithScopeFactory<T>() where T : class, IRabbitMqScopeFactory;
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
            _services.TryAddScoped<IRabbitMqScopeFactory, RabbitMqScopeFactory>();

            _services.AddHostedService<RabbitMqHostedService>();

            return this;
        }

        public IStreamFlowRabbitMq WithScopeFactory<T>() where T: class, IRabbitMqScopeFactory
        {
            _services.RemoveAll<IRabbitMqScopeFactory>();
            _services.AddScoped<IRabbitMqScopeFactory, T>();
            return this;
        }
    }
}
