namespace StreamFlow;

public interface IPublisher
{
    Task PublishAsync<T>(T message, PublishOptions? options = null, CancellationToken cancellationToken = default) where T : class;
}
