using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StreamFlow.Configuration;
using StreamFlow.Outbox;
using StreamFlow.RabbitMq.Connection;
using StreamFlow.RabbitMq.Hosting;
using StreamFlow.RabbitMq.Outbox;
using StreamFlow.RabbitMq.Publisher;
using StreamFlow.RabbitMq.Server;

namespace StreamFlow.RabbitMq
{
    public static class StreamFlowBuilderExtensions
    {
        public static IStreamFlowTransport UseRabbitMq(this IStreamFlowTransport builder, Action<IStreamFlowRabbitMq> configure)
        {
            var rabbitMq = new StreamFlowRabbitMq(builder.Services, builder.Options);

            configure(rabbitMq);

            return builder;
        }
    }

    public interface IStreamFlowRabbitMq
    {
        IStreamFlowRabbitMq Connection(string hostName, string userName, string password, string virtualHost = "/");
        IStreamFlowRabbitMq Connection(string[] hostNames, string userName, string password, string virtualHost = "/");
        IStreamFlowRabbitMq EnableConsumerHost(Action<RabbitMqConsumerOptions>? options = null);
        IStreamFlowRabbitMq WithMetricsProvider<TMetrics>() where TMetrics : class, IRabbitMqMetrics;
        IStreamFlowRabbitMq WithPublisherOptions(Action<RabbitMqPublisherOptions> options);
    }

    internal class StreamFlowRabbitMq: IStreamFlowRabbitMq
    {
        private readonly IServiceCollection _services;
        private readonly StreamFlowOptions _options;
        private readonly RabbitMqPublisherOptions _publisherOptions;
        private readonly RabbitMqConsumerOptions _consumerOptions;

        public StreamFlowRabbitMq(IServiceCollection services, StreamFlowOptions options)
        {
            _services = services;
            _options = options;

            services.TryAddSingleton<IRabbitMqConventions, RabbitMqConventions>();
            services.TryAddSingleton<IMessageSerializer, RabbitMqMessageSerializer>();
            services.TryAddSingleton<IOutboxMessageAddressProvider, RabbitMqMessageAddressProvider>();
            services.TryAddScoped<ILoggerScopeStateFactory, LoggerScopeStateFactory>();
            services.TryAddSingleton<IRabbitMqMetrics, RabbitMqNoOpMetrics>();
            
            services.TryAddScoped<IPublisher, RabbitMqPublisher>();
            services.TryAddSingleton<IRabbitMqPublisherChannel, RabbitMqPublisherChannel>();
            services.TryAddSingleton<IRabbitMqPublisherBus, RabbitMqPublisherBus>();

            _publisherOptions = new RabbitMqPublisherOptions();
            services.AddSingleton(_publisherOptions);

            _consumerOptions = new RabbitMqConsumerOptions();
            services.AddSingleton(_consumerOptions);
        }

        public IStreamFlowRabbitMq Connection(string hostName, string userName, string password, string virtualHost = "/")
        {
            return Connection(new[] {hostName}, userName, password, virtualHost);
        }

        public IStreamFlowRabbitMq Connection(string[] hostNames, string userName, string password, string virtualHost = "/")
        {
            _services.AddSingleton<IRabbitMqConnection>(new RabbitMqConnection(hostNames, userName, password, virtualHost, _options.ServiceId));

            _services.TryAddSingleton<IRabbitMqPublisherConnection, RabbitMqPublisherConnection>();
            _services.TryAddSingleton<IRabbitMqErrorHandlerConnection, RabbitMqErrorHandlerConnection>();
            _services.TryAddSingleton<IRabbitMqServer, RabbitMqServer>();
            _services.TryAddSingleton<IRabbitMqServerController, RabbitMqServerController>();
            _services.TryAddTransient<IRabbitMqErrorHandler, RabbitMqErrorHandler>();

            return this;
        }

        public IStreamFlowRabbitMq EnableConsumerHost(Action<RabbitMqConsumerOptions>? options)
        {
            _services.AddHostedService<RabbitMqConsumerHostedService>();

            options?.Invoke(_consumerOptions);

            return this;
        }

        public IStreamFlowRabbitMq WithMetricsProvider<TMetrics>() where TMetrics: class, IRabbitMqMetrics
        {
            var descriptor = new ServiceDescriptor(typeof(IRabbitMqMetrics), typeof(TMetrics), ServiceLifetime.Singleton);
            _services.Replace(descriptor);

            return this;
        }

        public IStreamFlowRabbitMq WithPublisherOptions(Action<RabbitMqPublisherOptions> options)
        {
            options?.Invoke(_publisherOptions);

            if (_publisherOptions.IsPublisherHostEnabled)
            {
                _services.AddHostedService<RabbitMqPublisherHostedService>();
            }

            if (_publisherOptions.PoolOptions != null)
            {
                _services.AddSingleton(_publisherOptions.PoolOptions);
                _services.TryAddSingleton<RabbitMqPublisherPool>();

                _services.TryAddSingleton<IRabbitMqPublisherPool>(p => p.GetRequiredService<RabbitMqPublisherPool>());
                _services.TryAddSingleton<IRabbitMqPublisherPoolController>(p => p.GetRequiredService<RabbitMqPublisherPool>());

                // this instance will be created by pool, so we need one new instance every time it is requested
                _services.TryAddTransient<RabbitMqPublisher>();

                // lets make IPublisher singleton, there is no need to be scoped here as we take publishers from singleton pool anyway
                var descriptor = new ServiceDescriptor(typeof(IPublisher), typeof(RabbitMqPublisherWithPooling), ServiceLifetime.Singleton);
                _services.Replace(descriptor);
            }

            return this;
        }
    }
}
