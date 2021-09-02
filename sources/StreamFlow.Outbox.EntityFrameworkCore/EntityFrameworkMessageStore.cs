using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Outbox.Entities;

namespace StreamFlow.Outbox.EntityFrameworkCore
{
    public class EntityFrameworkMessageStore<TContext> : IOutboxMessageStore
        where TContext : DbContext, IOutboxMessageContext
    {
        private readonly IServiceProvider _services;

        public EntityFrameworkMessageStore(IServiceProvider services)
        {
            _services = services;
        }

        public async Task SaveAsync<T>(OutboxMessage outboxMessage, bool triggerSaveChanges, CancellationToken cancellationToken = default)
            where T : class
        {
            var context = _services.GetRequiredService<TContext>();
            await context.Messages
                .AddAsync(outboxMessage, cancellationToken)
                .ConfigureAwait(false);

            if (triggerSaveChanges)
            {
                await context
                    .SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async Task<IAsyncDisposable?> StartLock(string key)
        {
            var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var transaction = await context.Database.BeginTransactionAsync();

            context.Locks.Add(new OutboxLock
            {
                OutboxLockId = ToSha256(key)
            });

            await context.SaveChangesAsync().ConfigureAwait(false);

            return new OutboxLockContainer(scope, transaction);
        }

        public async Task<IReadOnlyCollection<OutboxMessage>> LoadAsync(CancellationToken cancellationToken)
        {
            var context = _services.GetRequiredService<TContext>();
            return await context.Messages
                .Where(m => m.Scheduled < DateTime.UtcNow && m.Published == null)
                .OrderBy(m => m.Created)
                .ToListAsync(cancellationToken);
        }

        public async Task MarkAsPublished(OutboxMessage message, CancellationToken cancellationToken)
        {
            var context = _services.GetRequiredService<TContext>();
            message.Published = DateTime.UtcNow;
            context.Messages.Update(message);
            await context.SaveChangesAsync(cancellationToken);
        }

        private string ToSha256(string key)
        {
            var bytes = Encoding.Unicode.GetBytes(key);

            using var sha256 = new SHA256Managed();
            var hash = sha256.ComputeHash(bytes);
            var hashString = string.Empty;
            foreach (byte x in hash)
            {
                hashString += $"{x:x2}";
            }

            return hashString;
        }

        private class OutboxLockContainer : IAsyncDisposable
        {
            private readonly IServiceScope _scope;
            private readonly IDbContextTransaction _transaction;

            public OutboxLockContainer(IServiceScope scope, IDbContextTransaction transaction)
            {
                _scope = scope;
                _transaction = transaction;
            }

            public async ValueTask DisposeAsync()
            {
                await _transaction.RollbackAsync().ConfigureAwait(false);
                await _transaction.DisposeAsync().ConfigureAwait(false);

                _scope.Dispose();
            }
        }
    }
}
