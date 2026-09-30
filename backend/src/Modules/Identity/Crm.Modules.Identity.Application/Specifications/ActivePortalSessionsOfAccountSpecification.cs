using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Domain;

namespace Crm.Modules.Identity.Application.Specifications;

/// <summary>Tracked: a password change revokes every live session of one portal account.</summary>
public sealed class ActivePortalSessionsOfAccountSpecification : Specification<PortalSession>
{
    public ActivePortalSessionsOfAccountSpecification(PortalAccountId accountId)
    {
        ArgumentNullException.ThrowIfNull(accountId);

        Where(s => s.PortalAccountId == accountId && s.RevokedAtUtc == null);
    }
}
