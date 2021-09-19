using Microsoft.EntityFrameworkCore;
using StreamFlow.Outbox.Entities;
using StreamFlow.Outbox.EntityFrameworkCore;

namespace StreamFlow.Tests.AspNetCore.Database
{
    // Add-Migration -Name Init -Project StreamFlow.Tests.AspNetCore -StartUpProject StreamFlow.Tests.AspNetCore -Output Database
    public class ApplicationDbContext: DbContext, IOutboxMessageContext
    {
        public DbSet<OutboxMessage> Messages { get; set; }
        public DbSet<OutboxLock> Locks { get; set;  }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyStreamFlowConsumerConfiguration();
        }
    }
}
