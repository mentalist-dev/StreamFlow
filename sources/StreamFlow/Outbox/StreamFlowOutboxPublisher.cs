using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StreamFlow.Outbox
{
    internal class StreamFlowOutboxPublisher: BackgroundService
    {
        public static AutoResetEvent Continue { get; } = new(true);

        private readonly StreamFlowOutboxPublisherOptions _options;
        private readonly IServiceProvider _services;
        private readonly ILogger<StreamFlowOutboxPublisher> _logger;

        public StreamFlowOutboxPublisher(StreamFlowOutboxPublisherOptions options, IServiceProvider services, ILogger<StreamFlowOutboxPublisher> logger)
        {
            _options = options;
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    int publishedMessages;
                    do
                    {
                        publishedMessages = await PublishMessages(stoppingToken).ConfigureAwait(false);
                    } while (publishedMessages > 0);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error occurred when publishing messages");
                }

                Continue.WaitOne(_options.SleepDuration);
            }

            await PublishMessages(stoppingToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            // signal shutdown
            Continue.Set();
            return base.StopAsync(cancellationToken);
        }

        private async Task<int> PublishMessages(CancellationToken stoppingToken)
        {
            var count = 0;

            using var scope = _services.CreateScope();

            var store = scope.ServiceProvider.GetRequiredService<IOutboxMessageStore>();

            await using var _ = await store.StartLock(GetType().Name).ConfigureAwait(false);

            var messages = await store.LoadAsync(stoppingToken).ConfigureAwait(false);
            if (messages.Count > 0)
            {
                var serializer = scope.ServiceProvider.GetRequiredService<IMessageSerializer>();
                var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                foreach (var outboxMessage in messages)
                {
                    var message = outboxMessage.Body;
                    var options = outboxMessage.Options != null
                        ? serializer.Deserialize<PublishOptions>(outboxMessage.Options)
                        : null;

                    if (options?.Exchange == null)
                    {
                        options ??= new PublishOptions();
                        options.Exchange = outboxMessage.TargetAddress;
                    }

                    await publisher
                        .PublishAsync(message, options)
                        .ConfigureAwait(false);

                    await store.MarkAsPublished(outboxMessage, CancellationToken.None);

                    count += 1;
                }
            }

            return count;
        }
    }
}
