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
