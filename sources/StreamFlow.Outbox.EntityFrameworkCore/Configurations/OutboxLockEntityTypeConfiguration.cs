using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StreamFlow.Outbox.Entities;

namespace StreamFlow.Outbox.EntityFrameworkCore.Configurations
{
    public class OutboxLockEntityTypeConfiguration : IEntityTypeConfiguration<OutboxLock>
    {
        public void Configure(EntityTypeBuilder<OutboxLock> builder)
        {
            builder.ToTable("Locks", "StreamFlow");
            builder.HasKey(b => b.OutboxLockId);
            builder
                .Property(x => x.OutboxLockId)
                .HasMaxLength(100)
                .IsRequired();
        }
    }
}
