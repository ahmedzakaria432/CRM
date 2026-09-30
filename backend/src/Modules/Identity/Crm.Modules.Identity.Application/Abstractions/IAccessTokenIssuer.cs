namespace Crm.Modules.Identity.Application.Abstractions;

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(AccessTokenSubject subject, DateTimeOffset nowUtc);

    /// <summary>Story 47: the portal customer token (audience crm-portal).</summary>
    IssuedAccessToken IssuePortal(PortalAccessTokenSubject subject, DateTimeOffset nowUtc);
}

public sealed record AccessTokenSubject(
    Guid UserId,
    string DisplayName,
    Guid TenantId,
    Guid SessionId,
    IReadOnlyList<TokenMembership> Memberships);

public sealed record TokenMembership(Guid DepartmentId, Guid BranchId, string Role);

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>Story 47: a portal token carries no memberships and no roles, only the customer grant.</summary>
public sealed record PortalAccessTokenSubject(
    Guid AccountId,
    string DisplayName,
    Guid TenantId,
    Guid SessionId,
    Guid CustomerId,
    Guid ContactId,
    bool IsAccountAdmin,
    Guid BranchId,
    Guid DepartmentId);
