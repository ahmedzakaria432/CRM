using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crm.BuildingBlocks.Infrastructure.Persistence;

/// <summary>The most security-relevant code in the system. Kept in this one file, and nowhere else.</summary>
public abstract partial class ModuleDbContext
{
    /// <summary>Second NAMED filter next to SoftDeleteFilterName (Story 02 rule: never the unnamed overload - it
    /// would replace the soft-delete filter). Lifted only by Specification.IgnoreOrganisationScope().</summary>
    public const string ScopeFilterName = "OrganisationScope";

    // Read by EF at query time from the CURRENT context instance (R2). Never cache these in fields.
    private OrganisationScope Scope => currentUser.Scope;

    private bool ScopeIsUnrestricted => Scope.IsUnrestricted;

    private TenantId ScopeTenantId => Scope.Tenant;

    private IReadOnlyList<BranchId> ScopeBranchIds => Scope.Branches;

    private IReadOnlyList<DepartmentId> ScopeDepartmentIds => Scope.Departments;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Every module maps the scope ids to uniqueidentifier the same way (R1 relies on convention converters).
        configurationBuilder.Properties<TenantId>().HaveConversion<TenantIdConverter>();
        configurationBuilder.Properties<BranchId>().HaveConversion<BranchIdConverter>();
        configurationBuilder.Properties<DepartmentId>().HaveConversion<DepartmentIdConverter>();

        base.ConfigureConventions(configurationBuilder);
    }

    private void ApplyOrganisationScopeFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes().Where(t => !t.IsOwned()).ToList())
        {
            string? method = typeof(IOrganisationScoped).IsAssignableFrom(entityType.ClrType) ? nameof(FilterOrganisationScoped)
                : typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType) ? nameof(FilterTenantScoped)
                : null;

            if (method is null)
            {
                continue;
            }

            typeof(ModuleDbContext).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private void FilterOrganisationScoped<T>(ModelBuilder modelBuilder) where T : class, IOrganisationScoped =>
        modelBuilder.Entity<T>().HasQueryFilter(ScopeFilterName, (T e) =>
            ScopeIsUnrestricted
            || (e.TenantId.Equals(ScopeTenantId)
                && ScopeBranchIds.Contains(e.BranchId)
                && ScopeDepartmentIds.Contains(e.DepartmentId)));

    private void FilterTenantScoped<T>(ModelBuilder modelBuilder) where T : class, ITenantScoped =>
        modelBuilder.Entity<T>().HasQueryFilter(ScopeFilterName, (T e) => ScopeIsUnrestricted || e.TenantId.Equals(ScopeTenantId));

    /// <summary>Write guard: no Added/Modified scoped row may leave the caller's scope - covers a create that names
    /// a foreign department and a change that moves a row. Rows are never loaded out of scope, so this is the
    /// backstop, not the main path. A Modified ITransferableOrganisationScoped row is judged by its original scope
    /// (Story 15 transfer).</summary>
    private void EnsureWritesInScope()
    {
        if (Scope.IsUnrestricted)
        {
            return;
        }

        foreach (EntityEntry<ITenantScoped> entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.State == EntityState.Modified && entry.Entity is ITransferableOrganisationScoped && WasInScopeBeforeChange(entry))
            {
                continue; // a transfer: the row was the caller's, stays in the tenant, and may leave the caller's scope
            }

            if (!Scope.Allows(entry.Entity))
            {
                throw new OrganisationScopeViolationException(entry.Entity.GetType().Name, "Write");
            }
        }
    }

    /// <summary>Original (loaded) scope columns in the caller's scope, and the tenant unchanged. OriginalValue is the
    /// CLR property value (TenantId/BranchId/DepartmentId), before conversion.</summary>
    private bool WasInScopeBeforeChange(EntityEntry<ITenantScoped> entry)
    {
        if (entry.Property(nameof(ITenantScoped.TenantId)).OriginalValue is not TenantId originalTenant
            || entry.Property(nameof(IOrganisationScoped.BranchId)).OriginalValue is not BranchId originalBranch
            || entry.Property(nameof(IOrganisationScoped.DepartmentId)).OriginalValue is not DepartmentId originalDepartment)
        {
            return false;
        }

        return originalTenant.Equals(entry.Entity.TenantId)
            && Scope.Allows(new OriginalScope(originalTenant, originalBranch, originalDepartment));
    }

    private sealed record OriginalScope(TenantId TenantId, BranchId BranchId, DepartmentId DepartmentId) : IOrganisationScoped;

    private sealed class TenantIdConverter() : ValueConverter<TenantId, Guid>(v => v.Value, v => new TenantId(v));

    private sealed class BranchIdConverter() : ValueConverter<BranchId, Guid>(v => v.Value, v => new BranchId(v));

    private sealed class DepartmentIdConverter() : ValueConverter<DepartmentId, Guid>(v => v.Value, v => new DepartmentId(v));
}
