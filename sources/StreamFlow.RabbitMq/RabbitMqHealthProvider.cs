using StreamFlow.Configuration;
using StreamFlow.RabbitMq.Publisher;
using StreamFlow.RabbitMq.Server;

namespace StreamFlow.RabbitMq;

public interface IRabbitMqHealthProvider
{
    RabbitMqHealth Get();
}

public class RabbitMqHealth
{
    public bool IsServerReady { get; }
    public bool IsServerRunning { get; }
    public bool IsPublishersRunning { get; }

    public RabbitMqHealth(bool isServerReady, bool isServerRunning, bool isPublishersRunning)
    {
        IsServerReady = isServerReady;
        IsServerRunning = isServerRunning;
        IsPublishersRunning = isPublishersRunning;
    }
}

internal class RabbitMqHealthProvider: IRabbitMqHealthProvider
{
    private readonly IRabbitMqServer _server;
    private readonly IRabbitMqPublisherConnection _publisherConnection;
    private readonly IConsumerRegistrations _registrations;

    public RabbitMqHealthProvider(IRabbitMqServer server, IRabbitMqPublisherConnection publisherConnection, IConsumerRegistrations registrations)
    {
        _server = server;
        _publisherConnection = publisherConnection;
        _registrations = registrations;
    }

    public RabbitMqHealth Get()
    {
        var state = _server.GetState();

        var isServerReady = state.Registrations == _registrations.Consumers.Count;
        var isServerRunning = state.IsConnected;
        var isPublishersRunning = _publisherConnection.IsConnected ?? true;

        return new RabbitMqHealth(isServerReady, isServerRunning, isPublishersRunning);
    }
}
