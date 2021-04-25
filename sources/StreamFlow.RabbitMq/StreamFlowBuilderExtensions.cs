using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StreamFlow.Configuration;
using StreamFlow.RabbitMq.Connection;
using StreamFlow.RabbitMq.Server;
using StreamFlow.RabbitMq.Server.Hosting;

namespace StreamFlow.RabbitMq
{
    public static class StreamFlowBuilderExtensions
    {
        public static IStreamFlowTransport UsingRabbitMq(this IStreamFlowTransport builder, Action<IStreamFlowRabbitMq> configure)
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
        IStreamFlowRabbitMq Connection(string hostName, string userName, string password, string virtualHost = "/");
        IStreamFlowRabbitMq StartConsumerHostedService();
    }

    internal class StreamFlowRabbitMq: IStreamFlowRabbitMq
    {
        private readonly IServiceCollection _services;

        public StreamFlowRabbitMq(IServiceCollection services)
        {
            _services = services;
        }

        public IStreamFlowRabbitMq Connection(string hostName, string userName, string password, string virtualHost = "/")
        {
            _services.AddSingleton<IRabbitMqConnection>(new RabbitMqConnection(hostName, userName, password, virtualHost));
            _services.AddSingleton<IRabbitMqServer, RabbitMqServer>();
            _services.AddSingleton<IRabbitMqServerController, RabbitMqServerController>();
            _services.AddTransient<IRabbitMqErrorHandler, RabbitMqErrorHandler>();

            return this;
        }

        public IStreamFlowRabbitMq StartConsumerHostedService()
        {
            _services.AddHostedService<RabbitMqHostedService>();

            return this;
        }
    }
}
