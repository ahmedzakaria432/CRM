using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Domain;

public sealed class Department : AuditableEntity<DepartmentId>, ITenantScoped, IHasRowVersion
{
    public TenantId TenantId { get; private set; } = TenantId.None;

    public BranchId BranchId { get; private set; } = new(Guid.Empty);

    public OrganisationCode Code { get; private set; }

    public LocalizedText Name { get; private set; }

    public bool IsActive { get; private set; } = true;

    public byte[] RowVersion { get; private set; } = [];

    private Department()
    {
        Code = null!;
        Name = null!;
    }

    private Department(DepartmentId id, TenantId tenantId, BranchId branchId, OrganisationCode code, LocalizedText name) : base(id)
    {
        TenantId = tenantId;
        BranchId = branchId;
        Code = code;
        Name = name;
    }

    /// <summary>Reading <paramref name="branch"/> in the factory is a read, not a second aggregate mutated in
    /// the same handler (CLAUDE.md, "one transaction, one aggregate"). Departments never move between branches
    /// - reparenting is out of scope for this story.</summary>
    public static Department Create(Branch branch, OrganisationCode code, LocalizedText name, DepartmentId? id = null)
    {
        ArgumentNullException.ThrowIfNull(branch);

        if (!branch.IsActive)
        {
            throw new DomainException(new DomainError("Organisation.BranchInactive", "The branch is inactive."));
        }

        return new Department(id ?? DepartmentId.New(), branch.TenantId, branch.Id, code, name);
    }

    public void Rename(LocalizedText name) => Name = name;

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new DomainException(new DomainError("Organisation.DepartmentAlreadyInactive", "The department is already inactive."));
        }

        IsActive = false;
    }
}
