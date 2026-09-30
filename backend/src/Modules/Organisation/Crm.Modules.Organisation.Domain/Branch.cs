using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Domain;

public sealed class Branch : AuditableEntity<BranchId>, ITenantScoped, IHasRowVersion
{
    public TenantId TenantId { get; private set; } = TenantId.None;

    public OrganisationCode Code { get; private set; }

    public LocalizedText Name { get; private set; }

    public bool IsActive { get; private set; } = true;

    public byte[] RowVersion { get; private set; } = [];

    private Branch()
    {
        Code = null!;
        Name = null!;
    }

    private Branch(BranchId id, TenantId tenantId, OrganisationCode code, LocalizedText name) : base(id)
    {
        TenantId = tenantId;
        Code = code;
        Name = name;
    }

    public static Branch Create(TenantId tenantId, OrganisationCode code, LocalizedText name, BranchId? id = null)
    {
        if (tenantId == TenantId.None)
        {
            throw new DomainException(new DomainError("Organisation.TenantRequired", "A branch must belong to a tenant."));
        }

        return new Branch(id ?? BranchId.New(), tenantId, code, name);
    }

    public void Rename(LocalizedText name) => Name = name;

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new DomainException(new DomainError("Organisation.BranchAlreadyInactive", "The branch is already inactive."));
        }

        IsActive = false;
    }
}
