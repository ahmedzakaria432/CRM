using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Domain;

namespace Crm.Modules.Identity.Application.Specifications;

/// <summary>Tracked: rotation and revocation write the family it finds.</summary>
public sealed class RefreshTokenFamilyByTokenHashSpecification : Specification<RefreshTokenFamily>
{
    public RefreshTokenFamilyByTokenHashSpecification(string tokenHash)
    {
        Where(f => f.Tokens.Any(t => t.TokenHash == tokenHash));
    }
}
