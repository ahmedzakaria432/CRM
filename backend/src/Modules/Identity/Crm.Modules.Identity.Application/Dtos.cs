using Crm.Modules.Identity.Domain;

namespace Crm.Modules.Identity.Application;

// Primitives only - no SharedKernel id types cross into a DTO (keeps Swagger/pnpm gen:api shapes plain).
public sealed record SessionTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

/// <summary>Succeeded = false still COMMITS (failed-attempt counter, reuse revocation) - a failed sign-in is a
/// committed fact, not a failed Result; the controller maps it to 401.</summary>
public sealed record SignInOutcome(SessionTokens? Tokens, string? FailureCode)
{
    public bool Succeeded => Tokens is not null;

    public static SignInOutcome Success(SessionTokens tokens) => new(tokens, null);

    public static SignInOutcome Failed(string code) => new(null, code);
}

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string PreferredLocale,
    bool IsActive,
    bool IsLockedOut,
    DateTimeOffset? LastSignInAtUtc,
    DateTimeOffset? CreatedAtUtc,
    byte[] RowVersion)
{
    public static UserDto FromDomain(User user, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserDto(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName.Value,
            user.PreferredLocale.Value,
            user.IsActive,
            user.IsLockedOut(nowUtc),
            user.LastSignInAtUtc,
            user.CreatedAtUtc,
            user.RowVersion);
    }
}

public sealed record MeMembershipDto(Guid DepartmentId, Guid BranchId, string Role);

public sealed record MeDto(
    Guid Id,
    string Email,
    string DisplayName,
    string PreferredLocale,
    Guid TenantId,
    Guid? ActiveDepartmentId,
    IReadOnlyList<MeMembershipDto> Memberships,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
