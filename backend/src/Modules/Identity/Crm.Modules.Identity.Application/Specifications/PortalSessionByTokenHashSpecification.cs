using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Domain;

namespace Crm.Modules.Identity.Application.Specifications;

/// <summary>Tracked: rotation and revocation write the session it finds.</summary>
public sealed class PortalSessionByTokenHashSpecification : Specification<PortalSession>
{
    public PortalSessionByTokenHashSpecification(string tokenHash)
    {
        Where(s => s.Tokens.Any(t => t.TokenHash == tokenHash));
    }
}
