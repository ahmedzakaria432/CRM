namespace Crm.Modules.Identity.Application.Abstractions;

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(AccessTokenSubject subject, DateTimeOffset nowUtc);
}

public sealed record AccessTokenSubject(
    Guid UserId,
    string DisplayName,
    Guid TenantId,
    Guid SessionId,
    IReadOnlyList<TokenMembership> Memberships);

public sealed record TokenMembership(Guid DepartmentId, Guid BranchId, string Role);

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);
