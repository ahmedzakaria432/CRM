using Crm.SharedKernel.Scoping;

namespace Crm.BuildingBlocks.Application.Abstractions;

public sealed record ScopeMembership(DepartmentId DepartmentId, BranchId BranchId, string Role);

public sealed record ScopeWriteTarget(TenantId TenantId, BranchId BranchId, DepartmentId DepartmentId);

/// <summary>The caller's data scope for one request. Built only by CurrentUserService; read by the
/// ModuleDbContext scope filter, the write guard, CachingBehavior (via ICurrentUserService strings), and the
/// Organisation module's "my scope" query.</summary>
public sealed class OrganisationScope
{
    public const string HeaderName = "X-Crm-Scope";
    public const string AllDepartments = "all";

    /// <summary>No HttpContext: seeders, outbox consumers, design-time tooling. Filters are a no-op.</summary>
    public static readonly OrganisationScope Unrestricted = new(true, TenantId.None, [], null, false);

    /// <summary>An HttpContext with no authenticated user. Sees no scoped row.</summary>
    public static readonly OrganisationScope None = new(false, TenantId.None, [], null, false);

    public OrganisationScope(
        bool isUnrestricted,
        TenantId tenant,
        IReadOnlyList<ScopeMembership> memberships,
        DepartmentId? activeDepartment,
        bool activeScopeRejected)
    {
        IsUnrestricted = isUnrestricted;
        Tenant = tenant;
        Memberships = memberships;
        ActiveDepartment = activeDepartment;
        ActiveScopeRejected = activeScopeRejected;

        if (activeScopeRejected)
        {
            Departments = [];
            Branches = [];
        }
        else if (activeDepartment is not null)
        {
            Departments = [activeDepartment];
            Branches = memberships
                .Where(m => m.DepartmentId == activeDepartment)
                .Select(m => m.BranchId)
                .Distinct()
                .OrderBy(b => b.Value)
                .ToList();
        }
        else
        {
            Departments = memberships.Select(m => m.DepartmentId).Distinct().OrderBy(d => d.Value).ToList();
            Branches = memberships.Select(m => m.BranchId).Distinct().OrderBy(b => b.Value).ToList();
        }

        Roles = memberships.Select(m => m.Role).Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToList();
    }

    public bool IsUnrestricted { get; }

    public TenantId Tenant { get; }

    public IReadOnlyList<ScopeMembership> Memberships { get; }

    /// <summary>Null = "all my departments".</summary>
    public DepartmentId? ActiveDepartment { get; }

    /// <summary>The X-Crm-Scope header named a department the caller is not a member of (or was malformed).
    /// OrganisationScopeMiddleware answers 403; Departments/Branches are then empty.</summary>
    public bool ActiveScopeRejected { get; }

    /// <summary>Effective departments: ActiveDepartment alone, or every membership's department. Sorted by Value.</summary>
    public IReadOnlyList<DepartmentId> Departments { get; }

    /// <summary>Branches of <see cref="Departments"/>, distinct, sorted by Value.</summary>
    public IReadOnlyList<BranchId> Branches { get; }

    public IReadOnlyList<string> Roles { get; }

    /// <summary>The one place the three-column rule is written in C#; the SQL rule in ModuleDbContext.OrganisationScope.cs
    /// must stay equivalent.</summary>
    public bool Allows(ITenantScoped row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (IsUnrestricted)
        {
            return true;
        }

        if (!row.TenantId.Equals(Tenant))
        {
            return false;
        }

        if (row is not IOrganisationScoped scoped)
        {
            return true;
        }

        return Branches.Contains(scoped.BranchId) && Departments.Contains(scoped.DepartmentId);
    }

    /// <summary>Where a new scoped row goes. <paramref name="requestedDepartment"/> wins when given and in scope;
    /// otherwise ActiveDepartment; otherwise the single effective department. Null when none resolves - the
    /// handler answers Forbidden (requested, out of scope) or DepartmentRequired (ambiguous).</summary>
    public ScopeWriteTarget? ResolveWriteTarget(Guid? requestedDepartment)
    {
        DepartmentId? target;

        if (requestedDepartment is { } requested)
        {
            var requestedId = new DepartmentId(requested);
            target = Departments.Contains(requestedId) ? requestedId : null;
        }
        else if (ActiveDepartment is not null)
        {
            target = ActiveDepartment;
        }
        else if (Departments.Count == 1)
        {
            target = Departments[0];
        }
        else
        {
            target = null;
        }

        if (target is null)
        {
            return null;
        }

        BranchId? branch = Memberships.Where(m => m.DepartmentId == target).Select(m => m.BranchId).FirstOrDefault();
        return branch is null ? null : new ScopeWriteTarget(Tenant, branch, target);
    }
}
