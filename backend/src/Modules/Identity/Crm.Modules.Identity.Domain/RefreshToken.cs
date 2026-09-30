using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

/// <summary>One issued refresh token, stored only as its SHA-256 hash. Child entity - no DbSet, no repository;
/// reachable only through <see cref="RefreshTokenFamily"/>.</summary>
public sealed class RefreshToken : Entity<Guid>
{
    public string TokenHash { get; private set; }

    public DateTimeOffset IssuedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RotatedAtUtc { get; private set; }

    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    internal RefreshToken(string tokenHash, DateTimeOffset issuedAtUtc, DateTimeOffset expiresAtUtc) : base(Guid.NewGuid())
    {
        TokenHash = tokenHash;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    internal void MarkRotated(DateTimeOffset nowUtc) => RotatedAtUtc = nowUtc;
}
