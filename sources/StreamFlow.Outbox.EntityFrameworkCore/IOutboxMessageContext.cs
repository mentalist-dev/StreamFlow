using Microsoft.EntityFrameworkCore;
using StreamFlow.Outbox.Entities;

namespace StreamFlow.Outbox.EntityFrameworkCore
{
    public interface IOutboxMessageContext
    {
        DbSet<OutboxMessage> Messages { get; }
        DbSet<OutboxLock> Locks { get; }
    }
}
