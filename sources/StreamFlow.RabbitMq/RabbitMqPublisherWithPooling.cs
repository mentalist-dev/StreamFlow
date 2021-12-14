namespace StreamFlow.RabbitMq;

internal class RabbitMqPublisherWithPooling : IPublisher
{
    private readonly IRabbitMqPublisherPool _pool;

    public RabbitMqPublisherWithPooling(IRabbitMqPublisherPool pool)
    {
        _pool = pool;
    }

    public async Task<PublishResponse> PublishAsync<T>(T message, PublishOptions? options = null) where T : class
    {
        var publisher = _pool.Get();
        try
        {
            return await publisher.PublishAsync(message, options).ConfigureAwait(false);
        }
        finally
        {
            _pool.Return(publisher);
        }
    }
}
