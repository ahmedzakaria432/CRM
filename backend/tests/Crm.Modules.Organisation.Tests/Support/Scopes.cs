using Crm.BuildingBlocks.Application.Abstractions;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Support;

/// <summary>Real <see cref="OrganisationScope"/> instances (the class is sealed with a public constructor).</summary>
public static class Scopes
{
    public static ScopeMembership Member(DepartmentId department, BranchId branch, string role = CrmRoles.Agent) =>
        new(department, branch, role);

    public static OrganisationScope Agent(TenantId tenant, params ScopeMembership[] memberships) =>
        new(false, tenant, memberships, null, false);

    public static OrganisationScope AgentOfTenantA(params ScopeMembership[] memberships) => Agent(Ids.TenantA, memberships);

    public static OrganisationScope Narrowed(TenantId tenant, IReadOnlyList<ScopeMembership> memberships, DepartmentId active) =>
        new(false, tenant, memberships, active, false);
}
