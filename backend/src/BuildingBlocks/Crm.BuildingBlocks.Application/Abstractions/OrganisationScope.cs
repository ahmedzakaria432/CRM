using Crm.SharedKernel.Scoping;

namespace Crm.BuildingBlocks.Application.Abstractions;

public sealed record ScopeMembership(DepartmentId DepartmentId, BranchId BranchId, string Role);

public sealed record ScopeWriteTarget(TenantId TenantId, BranchId BranchId, DepartmentId DepartmentId);

/// <summary>Story 47: what a portal customer principal may reach. Built only by CurrentUserService from a Portal-scheme
/// token. HomeBranchId/HomeDepartmentId = the customer's owning branch/department (where its new tickets go).</summary>
public sealed record PortalCustomerGrant(
    Guid CustomerId, Guid ContactId, bool IsAccountAdmin, BranchId HomeBranchId, DepartmentId HomeDepartmentId);

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

        EffectiveRoles = memberships
            .Where(m => Departments.Contains(m.DepartmentId))
            .Select(m => m.Role)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();
    }

    private OrganisationScope(TenantId tenant, PortalCustomerGrant grant)
    {
        IsUnrestricted = false;
        Tenant = tenant;
        Memberships = [];
        ActiveDepartment = null;
        ActiveScopeRejected = false;
        Departments = [];
        Branches = [];
        Roles = [CrmRoles.PortalCustomer];
        EffectiveRoles = [CrmRoles.PortalCustomer];
        PortalCustomer = grant;
    }

    /// <summary>Story 47 (D2): a portal customer principal - a tenant and a grant, no memberships, departments or
    /// branches. Sees tenant-scoped rows of its tenant and IPortalCustomerOwned rows of its own customer only.</summary>
    public static OrganisationScope ForPortalCustomer(TenantId tenant, PortalCustomerGrant grant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(grant);

        if (tenant == TenantId.None)
        {
            throw new ArgumentException("A portal customer belongs to a tenant.", nameof(tenant));
        }

        if (grant.CustomerId == Guid.Empty || grant.ContactId == Guid.Empty)
        {
            throw new ArgumentException("A portal customer grant names a customer and a contact.", nameof(grant));
        }

        return new OrganisationScope(tenant, grant);
    }

    /// <summary>Set only for a portal customer principal (Story 47); null for staff, anonymous and system callers.</summary>
    public PortalCustomerGrant? PortalCustomer { get; }

    public bool IsPortalCustomer => PortalCustomer is not null;

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

    /// <summary>Roles of the memberships inside <see cref="Departments"/> - what effective permissions follow
    /// (Story 07). Empty when the active scope was rejected.</summary>
    public IReadOnlyList<string> EffectiveRoles { get; }

    /// <summary>The one place the three-column rule is written in C#; the C# twin of both SQL rules in
    /// ModuleDbContext.OrganisationScope.cs, which must stay equivalent.</summary>
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

        if (row is IPortalCustomerOwned owned && PortalCustomer is { } grant)
        {
            return owned.CustomerId == grant.CustomerId; // Story 47 (D2): a portal principal reaches its own customer's rows only
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
