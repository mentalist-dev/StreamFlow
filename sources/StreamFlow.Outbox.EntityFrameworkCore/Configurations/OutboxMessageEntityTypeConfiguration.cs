using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StreamFlow.Outbox.Entities;

namespace StreamFlow.Outbox.EntityFrameworkCore.Configurations
{
    public class OutboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.ToTable("Messages", "StreamFlow");
            builder.HasKey(b => b.OutboxMessageId);
            builder.Property(x => x.OutboxMessageId).IsRequired();
            builder.Property(x => x.Created).IsRequired();
            builder.Property(x => x.Scheduled).IsRequired();
            builder.Property(x => x.Published).IsRequired(false);
            builder.Property(x => x.TargetAddress).HasMaxLength(1000).IsRequired();
            builder.Property(x => x.Body).IsRequired();
            builder.Property(x => x.Options).IsRequired(false);

            builder
                .HasIndex(m => new
                {
                    m.Created,
                    m.Scheduled,
                    m.Published
                })
                .HasDatabaseName("IX__Messages__Created__Scheduled__Published");
        }
    }
}
