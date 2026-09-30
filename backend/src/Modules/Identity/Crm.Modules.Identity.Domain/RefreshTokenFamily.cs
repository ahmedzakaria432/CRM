using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

/// <summary>One sign-in (a session): the chain of refresh tokens issued by rotating it. Its id is the `sid`
/// claim. Presenting an already-rotated token revokes the whole family.</summary>
public sealed class RefreshTokenFamily : AuditableEntity<RefreshTokenFamilyId>, IHasRowVersion
{
    private readonly List<RefreshToken> _tokens = [];

    public UserId UserId { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public SessionRevocationReason? RevocationReason { get; private set; }

    public IReadOnlyCollection<RefreshToken> Tokens => _tokens.AsReadOnly();

    public byte[] RowVersion { get; private set; } = [];

    public bool IsRevoked => RevokedAtUtc is not null;

    private RefreshTokenFamily()
    {
        UserId = null!;
    }

    private RefreshTokenFamily(RefreshTokenFamilyId id, UserId userId) : base(id)
    {
        UserId = userId;
    }

    public static RefreshTokenFamily Start(
        UserId userId,
        string tokenHash,
        DateTimeOffset nowUtc,
        DateTimeOffset expiresAtUtc,
        RefreshTokenFamilyId? id = null)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException(new DomainError("Identity.RefreshTokenRequired", "A session must start with a refresh token."));
        }

        if (expiresAtUtc <= nowUtc)
        {
            throw new DomainException(new DomainError("Identity.RefreshTokenExpiryInvalid", "A refresh token must expire in the future."));
        }

        var family = new RefreshTokenFamily(id ?? RefreshTokenFamilyId.New(), userId);
        family._tokens.Add(new RefreshToken(tokenHash, nowUtc, expiresAtUtc));
        family.Raise(new SessionStarted(userId, family.Id, nowUtc));
        return family;
    }

    /// <summary>Rotated: the presented token is the family's current one and unexpired - it is marked rotated and
    /// <paramref name="newHash"/> is appended. ReuseDetected: the presented token was already rotated, so the
    /// family is revoked. Expired / Revoked: nothing changes.</summary>
    public RefreshTokenRotation Rotate(string presentedHash, string newHash, DateTimeOffset nowUtc, DateTimeOffset newExpiresAtUtc)
    {
        if (IsRevoked)
        {
            return RefreshTokenRotation.Revoked;
        }

        RefreshToken? token = _tokens.FirstOrDefault(t => string.Equals(t.TokenHash, presentedHash, StringComparison.Ordinal));

        if (token is null)
        {
            return RefreshTokenRotation.Revoked; // defensive: the specification loads the family by hash
        }

        if (token.RotatedAtUtc is not null)
        {
            Revoke(SessionRevocationReason.ReuseDetected, nowUtc);
            return RefreshTokenRotation.ReuseDetected;
        }

        if (token.ExpiresAtUtc <= nowUtc)
        {
            return RefreshTokenRotation.Expired;
        }

        if (string.IsNullOrWhiteSpace(newHash) || newExpiresAtUtc <= nowUtc)
        {
            throw new DomainException(new DomainError("Identity.RefreshTokenRequired", "A rotation needs a new refresh token that expires in the future."));
        }

        token.MarkRotated(nowUtc);
        _tokens.Add(new RefreshToken(newHash, nowUtc, newExpiresAtUtc));
        return RefreshTokenRotation.Rotated;
    }

    /// <summary>Idempotent: a revoked family stays revoked with its first reason.</summary>
    public void Revoke(SessionRevocationReason reason, DateTimeOffset nowUtc)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAtUtc = nowUtc;
        RevocationReason = reason;
    }
}
