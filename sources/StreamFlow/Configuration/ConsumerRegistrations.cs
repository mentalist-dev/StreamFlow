using StreamFlow.Server;

namespace StreamFlow.Configuration;

public interface IConsumerRegistrations
{
    IList<IConsumerRegistration> Consumers { get; }
}

public class ConsumerRegistrations: IConsumerRegistrations
{
    private readonly IList<IConsumerRegistration> _registrations = new List<IConsumerRegistration>();

    public void Add(IConsumerRegistration consumerRegistration)
    {
        _registrations.Add(consumerRegistration);
    }

    public IList<IConsumerRegistration> Consumers => _registrations;
}
