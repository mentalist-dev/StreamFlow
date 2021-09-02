using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StreamFlow.Configuration;

namespace StreamFlow.Outbox
{
    public static class OutboxServiceCollectionExtensions
    {
        public static IStreamFlowTransport WithOutboxSupport(this IStreamFlowTransport transport, Action<IStreamFlowOutbox> configure)
        {
            transport.Services.TryAddScoped<IOutboxPublisher, OutboxPublisher>();
            transport.Services.TryAddScoped<IOutboxMessageStore, PublisherOutboxMessageStore>();
            transport.Services.TryAddSingleton<IOutboxMessageAddressProvider, OutboxMessageAddressProvider>();

            var builder = new StreamFlowOutbox(transport);
            configure(builder);

            return transport;
        }
    }

    public interface IStreamFlowOutbox
    {
        IStreamFlowOutbox UsePublishingServer(StreamFlowOutboxPublisherOptions? options = null);
        IStreamFlowOutbox WithMessageStore<TDbContext>() where TDbContext: class, IOutboxMessageStore;
    }

    internal class StreamFlowOutbox: IStreamFlowOutbox
    {
        private readonly IStreamFlowTransport _transport;

        public StreamFlowOutbox(IStreamFlowTransport transport)
        {
            _transport = transport;
        }

        public IStreamFlowOutbox UsePublishingServer(StreamFlowOutboxPublisherOptions? options = null)
        {
            options ??= new StreamFlowOutboxPublisherOptions();

            _transport.Services.AddSingleton(options);
            _transport.Services.AddHostedService<StreamFlowOutboxPublisher>();
            return this;
        }

        public IStreamFlowOutbox WithMessageStore<TDbContext>() where TDbContext: class, IOutboxMessageStore
        {
            _transport.Services.AddScoped<IOutboxMessageStore, TDbContext>();
            return this;
        }
    }
}
