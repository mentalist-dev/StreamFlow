using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StreamFlow.Outbox.Entities;

namespace StreamFlow.Outbox
{
    public interface IOutboxMessageStore
    {
        Task SaveAsync<T>(OutboxMessage outboxMessage, bool triggerSaveChanges, CancellationToken cancellationToken = default) where T : class;

        Task<IAsyncDisposable?> StartLock(string key);

        Task<IReadOnlyCollection<OutboxMessage>> LoadAsync(CancellationToken cancellationToken);
        Task MarkAsPublished(OutboxMessage message, CancellationToken cancellationToken);
    }

    internal class PublisherOutboxMessageStore : IOutboxMessageStore
    {
        private readonly IPublisher _publisher;
        private readonly IMessageSerializer _serializer;

        public PublisherOutboxMessageStore(IPublisher publisher, IMessageSerializer serializer)
        {
            _publisher = publisher;
            _serializer = serializer;
        }

        public Task SaveAsync<T>(OutboxMessage outboxMessage, bool triggerSaveChanges, CancellationToken cancellationToken = default) where T : class
        {
            var message = _serializer.Deserialize<T>(outboxMessage.Body)!;
            var options = outboxMessage.Options != null
                ? _serializer.Deserialize<PublishOptions>(outboxMessage.Options)
                : null;

            return _publisher.PublishAsync(message, options, cancellationToken);
        }

        public Task<IAsyncDisposable?> StartLock(string key)
        {
            return Task.FromResult((IAsyncDisposable?)null);
        }

        public Task<IReadOnlyCollection<OutboxMessage>> LoadAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<OutboxMessage> messages = Array.Empty<OutboxMessage>();
            return Task.FromResult(messages);
        }

        public Task MarkAsPublished(OutboxMessage message, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
