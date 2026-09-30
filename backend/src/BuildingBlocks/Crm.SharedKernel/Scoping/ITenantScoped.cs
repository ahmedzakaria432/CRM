namespace Crm.SharedKernel.Scoping;

/// <summary>Rows of one tenant. ModuleDbContext's named "OrganisationScope" filter restricts reads to the
/// caller's tenant and refuses to save a row of another tenant. Nothing to opt into.</summary>
public interface ITenantScoped
{
    TenantId TenantId { get; }
}

/// <summary>Operational rows of one department (and therefore one branch) of one tenant. Reads are restricted to
/// the caller's effective departments and branches; saves outside them are refused (403).</summary>
public interface IOrganisationScoped : ITenantScoped
{
    BranchId BranchId { get; }

    DepartmentId DepartmentId { get; }
}

/// <summary>An organisation-scoped row that one intention-revealing method may hand to another department of the
/// same tenant (Ticket.TransferTo, Story 15). The write guard judges a MODIFIED row of this kind by its ORIGINAL
/// tenant/branch/department, so the sender can move it out of their own scope; its tenant can never change, and an
/// ADDED row is judged like any other. Implement it only on an aggregate whose DepartmentId/BranchId have private
/// setters changed by exactly one method - adding an implementer is a security review.</summary>
public interface ITransferableOrganisationScoped : IOrganisationScoped;

/// <summary>Story 47: an organisation-scoped row that belongs to exactly one customer. A portal customer principal
/// (OrganisationScope.PortalCustomer) sees and may write such a row of its own tenant whose CustomerId equals its
/// grant's customer id - nothing else of the module. Implemented by Ticket only. CustomerId must be set once, never
/// Guid.Empty, and never change. Adding an implementer is a security review.</summary>
public interface IPortalCustomerOwned : IOrganisationScoped
{
    Guid CustomerId { get; }
}
