using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineShopping.Domain.Common;

namespace OnlineShopping.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps audit timestamps and turns deletes of <see cref="BaseEntity"/> into soft deletes.
/// </summary>
public sealed class AuditableEntityInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ApplyAuditRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditRules(DbContext? context)
    {
        if (context is null)
            return;

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedUtc = now;
                    entry.Entity.ModifiedUtc = null;
                    break;

                case EntityState.Modified:
                    // CreatedUtc is set once on insert and must never be overwritten by an update.
                    entry.Property(e => e.CreatedUtc).IsModified = false;
                    entry.Entity.ModifiedUtc = now;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Property(e => e.CreatedUtc).IsModified = false;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.ModifiedUtc = now;
                    break;
            }
        }
    }
}
