using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Domain;

/// <summary>The tenant itself. Unscoped - its id IS the tenant (CLAUDE.md, "Scoping": Organisation is not
/// ITenantScoped/IOrganisationScoped).</summary>
public sealed class Organisation : AuditableEntity<TenantId>, IHasRowVersion
{
    public OrganisationCode Code { get; private set; }

    public LocalizedText Name { get; private set; }

    public bool IsActive { get; private set; } = true;

    public byte[] RowVersion { get; private set; } = [];

    private Organisation()
    {
        Code = null!;
        Name = null!;
    }

    private Organisation(TenantId id, OrganisationCode code, LocalizedText name) : base(id)
    {
        Code = code;
        Name = name;
    }

    public static Organisation Create(OrganisationCode code, LocalizedText name, TenantId? id = null) =>
        new(id ?? new TenantId(Guid.NewGuid()), code, name);
}
