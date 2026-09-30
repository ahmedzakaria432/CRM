using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Organisation.Contracts;

namespace Crm.Modules.Identity.Application;

internal static class AccessTokenSubjectFactory
{
    /// <summary>Keeps the memberships only when they belong to the user's tenant (or there are none); a mismatch
    /// drops them all - defensive: the user then sees nothing rather than another tenant's data.</summary>
    public static AccessTokenSubject Build(User user, UserMemberships memberships, RefreshTokenFamilyId sessionId)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(memberships);
        ArgumentNullException.ThrowIfNull(sessionId);

        bool sameTenant = memberships.Memberships.Count == 0 || memberships.TenantId == user.TenantId.Value;

        List<TokenMembership> tokenMemberships = sameTenant
            ? [.. memberships.Memberships.Select(m => new TokenMembership(m.DepartmentId, m.BranchId, m.Role))]
            : [];

        return new AccessTokenSubject(
            user.Id.Value, user.DisplayName.Value, user.TenantId.Value, sessionId.Value, tokenMemberships);
    }
}
