using Crm.BuildingBlocks.Application.Abstractions;
using Crm.SharedKernel.Scoping;

namespace Crm.BuildingBlocks.Tests.Support;

/// <summary>Real <see cref="OrganisationScope"/> instances. The class is sealed with a public constructor, so
/// tests build the real thing instead of mocking it.</summary>
public static class Scopes
{
    public static readonly Guid PortalCustomerGuid = new("cccccccc-0000-0000-0000-0000000000c1");
    public static readonly Guid PortalContactGuid = new("cccccccc-0000-0000-0000-0000000000c2");

    public static ScopeMembership Member(DepartmentId department, BranchId branch, string role = CrmRoles.Agent) =>
        new(department, branch, role);

    public static OrganisationScope Agent(params ScopeMembership[] memberships) =>
        new(false, ScopeFixtures.TenantA, memberships, null, false);

    public static OrganisationScope AgentIn(DepartmentId department, BranchId branch, string role = CrmRoles.Agent) =>
        Agent(Member(department, branch, role));

    public static OrganisationScope AgentOfTenant(TenantId tenant, params ScopeMembership[] memberships) =>
        new(false, tenant, memberships, null, false);

    public static OrganisationScope Narrowed(IReadOnlyList<ScopeMembership> memberships, DepartmentId activeDepartment) =>
        new(false, ScopeFixtures.TenantA, memberships, activeDepartment, false);

    public static OrganisationScope Rejected(IReadOnlyList<ScopeMembership> memberships) =>
        new(false, ScopeFixtures.TenantA, memberships, null, true);

    public static OrganisationScope Portal(TenantId tenant, Guid customerId) =>
        OrganisationScope.ForPortalCustomer(
            tenant,
            new PortalCustomerGrant(customerId, PortalContactGuid, false, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1));
}
