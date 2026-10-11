using Crm.BuildingBlocks.Application.Abstractions;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Tests.Support;

/// <summary>Real <see cref="OrganisationScope"/> instances (the class is sealed with a public constructor).</summary>
public static class Scopes
{
    public static ScopeMembership Member(DepartmentId department, BranchId branch, string role = CrmRoles.Agent) =>
        new(department, branch, role);

    public static OrganisationScope Agent(TenantId tenant, params ScopeMembership[] memberships) =>
        new(false, tenant, memberships, null, false);

    /// <summary>A caller of tenant A who belongs to department A1 only.</summary>
    public static OrganisationScope AgentA1() => Agent(Ids.TenantA, Member(Ids.DepartmentA1, Ids.BranchA1));

    /// <summary>A caller of tenant A who belongs to departments A1 and A2 with no active department chosen.</summary>
    public static OrganisationScope AgentA1AndA2() =>
        Agent(Ids.TenantA, Member(Ids.DepartmentA1, Ids.BranchA1), Member(Ids.DepartmentA2, Ids.BranchA2));

    public static OrganisationScope Narrowed(TenantId tenant, IReadOnlyList<ScopeMembership> memberships, DepartmentId active) =>
        new(false, tenant, memberships, active, false);

    /// <summary>A caller of tenant A who belongs to department A2 only.</summary>
    public static OrganisationScope AgentA2Only() => Agent(Ids.TenantA, Member(Ids.DepartmentA2, Ids.BranchA2));

    /// <summary>A caller of another tenant: sees none of tenant A's rows.</summary>
    public static OrganisationScope AgentB1() => Agent(Ids.TenantB, Member(Ids.DepartmentB1, Ids.BranchB1));

    public static PortalCustomerGrant Grant(
        Guid? customer = null, Guid? contact = null, bool admin = false, BranchId? branch = null, DepartmentId? department = null) =>
        new(customer ?? Ids.Customer1, contact ?? Ids.Contact1, admin, branch ?? Ids.BranchA1, department ?? Ids.DepartmentA1);

    public static OrganisationScope Portal(PortalCustomerGrant grant, TenantId? tenant = null) =>
        OrganisationScope.ForPortalCustomer(tenant ?? Ids.TenantA, grant);
}
