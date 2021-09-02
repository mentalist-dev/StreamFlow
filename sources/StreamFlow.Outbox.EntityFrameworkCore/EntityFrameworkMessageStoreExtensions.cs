using Microsoft.EntityFrameworkCore;

namespace StreamFlow.Outbox.EntityFrameworkCore
{
    public static class EntityFrameworkMessageStoreExtensions
    {
        public static IStreamFlowOutbox UseEntityFrameworkCore<TDbContext>(this IStreamFlowOutbox builder)
            where TDbContext : DbContext, IOutboxMessageContext
        {
            builder = builder
                .WithMessageStore<EntityFrameworkMessageStore<TDbContext>>();

            return builder;
        }
    }
}
