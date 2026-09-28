using Crm.BuildingBlocks.Application.Abstractions;
using Crm.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Crm.BuildingBlocks.Infrastructure.Persistence.Interceptors;

public sealed class AuditingSaveChangesInterceptor(
    ICurrentUserService currentUser,
    IDateTime dateTime) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not { } context)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        string user = currentUser.UserIdOrSystem;
        DateTimeOffset nowUtc = dateTime.UtcNow;

        SoftDelete(context, user, nowUtc); // MUST run first - it changes entry states Audit relies on.
        Audit(context, user, nowUtc);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Setting only the root's state to Modified corrupts the row: EF cascades an aggregate root
    /// into Deleted alongside every owned entry (its value objects, its owned child collection), and
    /// flipping just the root back leaves those owned entries Deleted - EF then writes NULL over
    /// their columns. Verified by probe: this threw a NOT NULL constraint violation on a required
    /// owned column and would silently blank a nullable one. The fix is Unchanged (which resets the
    /// whole entry), then marking only the three soft-delete columns dirty, then rescuing every
    /// owned entry EF cascaded into Deleted alongside the root.
    /// </summary>
    private static void SoftDelete(DbContext context, string user, DateTimeOffset nowUtc)
    {
        List<EntityEntry> roots = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && e.Entity is ISoftDeletable)
            .ToList();

        if (roots.Count == 0)
        {
            return;
        }

        foreach (EntityEntry entry in roots)
        {
            entry.State = EntityState.Unchanged;
            entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
            entry.Property(nameof(ISoftDeletable.DeletedAtUtc)).CurrentValue = nowUtc;
            entry.Property(nameof(ISoftDeletable.DeletedBy)).CurrentValue = user;
        }

        foreach (EntityEntry owned in context.ChangeTracker.Entries().ToList())
        {
            if (owned.State == EntityState.Deleted && owned.Metadata.IsOwned())
            {
                owned.State = EntityState.Unchanged;
            }
        }
    }

    /// <summary>
    /// Runs after SoftDelete, because SoftDelete moves entries out of Deleted and those rows must
    /// get a modified stamp too.
    /// </summary>
    /// <remarks>
    /// An owned same-table value object (e.g. SampleName via OwnsOne) is tracked as its OWN entry,
    /// separate from its owner. Changing only the owned value leaves the owner's own EntityEntry at
    /// Unchanged even though EF issues one UPDATE against their shared row - confirmed by manual
    /// verification: a Rename() that only replaced the owned SampleName left ModifiedBy/ModifiedAtUtc
    /// unset because the root's own entry never reached Modified. The reference-navigation check
    /// below catches that: if an IAuditable root is Unchanged but one of its owned references
    /// changed, it still gets stamped, and setting a property on an Unchanged entry promotes it to
    /// Modified so the stamp actually reaches the UPDATE statement.
    /// </remarks>
    private static void Audit(DbContext context, string user, DateTimeOffset nowUtc)
    {
        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IAuditable)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = user;
                entry.Property(nameof(IAuditable.CreatedAtUtc)).CurrentValue = nowUtc;
                continue;
            }

            bool ownedReferenceChanged = entry.References
                .Any(r => r.TargetEntry?.State is EntityState.Added or EntityState.Modified);

            if (entry.State == EntityState.Modified || (entry.State == EntityState.Unchanged && ownedReferenceChanged))
            {
                entry.Property(nameof(IAuditable.ModifiedBy)).CurrentValue = user;
                entry.Property(nameof(IAuditable.ModifiedAtUtc)).CurrentValue = nowUtc;
            }
        }
    }
}
