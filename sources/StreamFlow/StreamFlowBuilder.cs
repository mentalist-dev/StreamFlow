using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace StreamFlow
{
    public interface IStreamFlow
    {
        IServiceCollection Services { get; }

        IStreamFlow Consumes<TRequest, TConsumer>(ConsumerOptions? consumerOptions = null)
            where TConsumer : class, IConsumer<TRequest>;
    }

    public class StreamFlowBuilder : IStreamFlow
    {
        private readonly ConsumerRegistrations _registrations;
        private readonly IServiceCollection _services;

        public StreamFlowBuilder(IServiceCollection services, ConsumerRegistrations registrations)
        {
            _services = services;
            _registrations = registrations;
        }

        public IStreamFlow Consumes<TRequest, TConsumer>(ConsumerOptions? consumerOptions = null) where TConsumer : class, IConsumer<TRequest>
        {
            _services.AddTransient<TConsumer>();

            consumerOptions ??= new ConsumerOptions();

            var registration = new Registration<TRequest, TConsumer>(consumerOptions);

            _registrations.Add(registration);

            return this;
        }

        IServiceCollection IStreamFlow.Services => _services;
    }

    public interface IConsumerRegistrations
    {
        IList<IRegistration> Consumers { get; }
    }

    public class ConsumerRegistrations: IConsumerRegistrations
    {
        private readonly IList<IRegistration> _registrations = new List<IRegistration>();

        public void Add(IRegistration registration)
        {
            _registrations.Add(registration);
        }

        public IList<IRegistration> Consumers => _registrations;
    }
}
