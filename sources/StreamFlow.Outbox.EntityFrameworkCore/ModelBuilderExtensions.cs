using Microsoft.EntityFrameworkCore;
using StreamFlow.Outbox.Entities;

namespace StreamFlow.Outbox.EntityFrameworkCore
{
    public static class ModelBuilderExtensions
    {
        public static void ApplyStreamFlowConsumerConfiguration(this ModelBuilder builder, string schema = "StreamFlow", string messageTableName = "Messages", string lockTableName = "Locks")
        {
            builder.Entity<OutboxMessage>(entity =>
            {
                entity.ToTable(messageTableName, schema);
                entity.HasKey(b => b.OutboxMessageId);
                entity.Property(x => x.OutboxMessageId).IsRequired();
                entity.Property(x => x.Created).IsRequired();
                entity.Property(x => x.Scheduled).IsRequired();
                entity.Property(x => x.Published).IsRequired(false);
                entity.Property(x => x.TargetAddress).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.Body).IsRequired();
                entity.Property(x => x.Options).IsRequired(false);

                entity
                    .HasIndex(m => new
                    {
                        m.Created,
                        m.Scheduled,
                        m.Published
                    })
                    .HasDatabaseName("IX__Messages__Created__Scheduled__Published");
            });

            builder.Entity<OutboxLock>(entity =>
            {
                entity.ToTable(lockTableName, schema);
                entity.HasKey(b => b.OutboxLockId);
                entity
                    .Property(x => x.OutboxLockId)
                    .HasMaxLength(100)
                    .IsRequired();
            });
        }

        public static void ApplyStreamFlowProducerConfiguration(this ModelBuilder builder, string schema = "StreamFlow", string messageTableName = "Messages", string lockTableName = "Locks")
        {
            builder.Entity<OutboxMessage>(entity =>
            {
                entity.ToView(messageTableName, schema);
                entity.HasKey(b => b.OutboxMessageId);
                entity.Property(x => x.OutboxMessageId).IsRequired();
                entity.Property(x => x.Created).IsRequired();
                entity.Property(x => x.Scheduled).IsRequired();
                entity.Property(x => x.Published).IsRequired(false);
                entity.Property(x => x.TargetAddress).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.Body).IsRequired();
                entity.Property(x => x.Options).IsRequired(false);
            });

            builder.Entity<OutboxLock>(entity =>
            {
                entity.ToView(lockTableName, schema);
                entity.HasKey(b => b.OutboxLockId);
                entity
                    .Property(x => x.OutboxLockId)
                    .HasMaxLength(100)
                    .IsRequired();
            });
        }
    }
}
