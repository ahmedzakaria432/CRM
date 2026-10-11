using Crm.SharedKernel.Scoping;

namespace Crm.BuildingBlocks.Tests.Support;

// In-memory rows for OrganisationScope.Allows. The persistence fixtures (Persistence/Fixtures.cs) are the EF twins.

public sealed record TenantScopedRow(TenantId TenantId) : ITenantScoped;

public sealed record OrganisationScopedRow(TenantId TenantId, BranchId BranchId, DepartmentId DepartmentId) : IOrganisationScoped;

public sealed record PortalOwnedRow(TenantId TenantId, BranchId BranchId, DepartmentId DepartmentId, Guid CustomerId)
    : IPortalCustomerOwned;
