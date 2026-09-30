using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Domain;

namespace Crm.Modules.Identity.Application.Specifications;

/// <summary>Tracked: deactivation and password changes revoke every live session of one user.</summary>
public sealed class ActiveRefreshTokenFamiliesOfUserSpecification : Specification<RefreshTokenFamily>
{
    public ActiveRefreshTokenFamiliesOfUserSpecification(UserId userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        Where(f => f.UserId == userId && f.RevokedAtUtc == null);
    }
}
