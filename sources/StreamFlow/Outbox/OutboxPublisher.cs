using System;
using System.Threading;
using System.Threading.Tasks;
using StreamFlow.Outbox.Entities;

namespace StreamFlow.Outbox
{
    public class OutboxPublisher: IOutboxPublisher
    {
        private readonly IOutboxMessageStore _messageStore;
        private readonly IOutboxMessageAddressProvider _addressProvider;
        private readonly IMessageSerializer _serializer;

        public OutboxPublisher(IOutboxMessageStore messageStore, IOutboxMessageAddressProvider addressProvider, IMessageSerializer serializer)
        {
            _messageStore = messageStore;
            _addressProvider = addressProvider;
            _serializer = serializer;
        }

        public async Task PublishAsync<T>(T message, PublishOptions? options = null, SchedulingOptions? scheduling = null, bool triggerSaveChanges = true, CancellationToken cancellationToken = default) where T : class
        {
            var address = options?.TargetAddress;
            if (address == null)
            {
                address = _addressProvider.Get(message, options);
            }

            var now = DateTime.UtcNow;

            var scheduled = now;
            if (scheduling != null)
            {
                if (scheduling.ExactDateTime != null)
                {
                    scheduled = scheduling.ExactDateTime.Value;
                }
                else if (scheduling.RelativeFromNow != null && scheduling.RelativeFromNow > TimeSpan.Zero)
                {
                    scheduled = now.Add(scheduling.RelativeFromNow.Value);
                }

                if (scheduled < now)
                {
                    scheduled = now;
                }
            }

            var entity = new OutboxMessage
            {
                OutboxMessageId = Guid.NewGuid(),
                Created = now,
                Scheduled = scheduled,
                Published = null,
                TargetAddress = address,
                Body = _serializer
                    .Serialize(message)
                    .ToArray(),
                Options = options != null
                    ? _serializer.Serialize(options).ToArray()
                    : null
            };

            await _messageStore.SaveAsync<T>(entity, message.GetType(), triggerSaveChanges, cancellationToken).ConfigureAwait(false);

            StreamFlowOutboxPublisher.Continue.Set();
        }
    }
}
