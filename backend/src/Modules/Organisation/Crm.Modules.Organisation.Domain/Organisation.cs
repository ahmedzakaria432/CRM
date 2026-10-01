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

    /// <summary>null = the product defaults (tokens.css). Story 58, D1.</summary>
    public Branding? Branding { get; private set; }

    /// <summary>The version Revert restores; null is the product defaults. Meaningful only when CanRevertBranding.</summary>
    public Branding? PreviousBranding { get; private set; }

    public bool CanRevertBranding { get; private set; }

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

    /// <summary>Saving the branding already in effect changes nothing - the real previous version survives (D2).</summary>
    public void ChangeBranding(Branding branding)
    {
        ArgumentNullException.ThrowIfNull(branding);

        if (branding.Equals(Branding))
        {
            return;
        }

        PreviousBranding = Branding;
        Branding = branding;
        CanRevertBranding = true;
    }

    /// <summary>Swaps current and previous (D2): reverting twice re-applies.</summary>
    public void RevertBranding()
    {
        if (!CanRevertBranding)
        {
            throw new DomainException(new DomainError("Organisation.NoPreviousBranding", "There is no previous branding to revert to."));
        }

        (Branding, PreviousBranding) = (PreviousBranding, Branding);
    }
}
