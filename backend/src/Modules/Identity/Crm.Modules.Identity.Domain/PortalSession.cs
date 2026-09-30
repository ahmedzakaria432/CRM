using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

/// <summary>One portal sign-in (Story 47, D7). Mirrors RefreshTokenFamily; its id is the portal token's sid. Presenting
/// an already-rotated token revokes the whole session.</summary>
public sealed class PortalSession : AuditableEntity<PortalSessionId>, IHasRowVersion
{
    private readonly List<RefreshToken> _tokens = [];

    public PortalAccountId PortalAccountId { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public SessionRevocationReason? RevocationReason { get; private set; }

    public IReadOnlyCollection<RefreshToken> Tokens => _tokens.AsReadOnly();

    public byte[] RowVersion { get; private set; } = [];

    public bool IsRevoked => RevokedAtUtc is not null;

    private PortalSession()
    {
        PortalAccountId = null!;
    }

    private PortalSession(PortalSessionId id, PortalAccountId accountId) : base(id)
    {
        PortalAccountId = accountId;
    }

    public static PortalSession Start(
        PortalAccountId accountId,
        string tokenHash,
        DateTimeOffset nowUtc,
        DateTimeOffset expiresAtUtc,
        PortalSessionId? id = null)
    {
        ArgumentNullException.ThrowIfNull(accountId);

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException(new DomainError("Identity.RefreshTokenRequired", "A session must start with a refresh token."));
        }

        if (expiresAtUtc <= nowUtc)
        {
            throw new DomainException(new DomainError("Identity.RefreshTokenExpiryInvalid", "A refresh token must expire in the future."));
        }

        var session = new PortalSession(id ?? PortalSessionId.New(), accountId);
        session._tokens.Add(new RefreshToken(tokenHash, nowUtc, expiresAtUtc));
        session.Raise(new PortalSessionStarted(accountId, session.Id, nowUtc));
        return session;
    }

    /// <summary>Rotated: the presented token is the current one of the session and unexpired - it is marked rotated and
    /// <paramref name="newHash"/> is appended. ReuseDetected: the presented token was already rotated, so the
    /// session is revoked. Expired / Revoked: nothing changes.</summary>
    public RefreshTokenRotation Rotate(string presentedHash, string newHash, DateTimeOffset nowUtc, DateTimeOffset newExpiresAtUtc)
    {
        if (IsRevoked)
        {
            return RefreshTokenRotation.Revoked;
        }

        RefreshToken? token = _tokens.FirstOrDefault(t => string.Equals(t.TokenHash, presentedHash, StringComparison.Ordinal));

        if (token is null)
        {
            return RefreshTokenRotation.Revoked; // defensive: the specification loads the session by hash
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

    /// <summary>Idempotent: a revoked session stays revoked with its first reason.</summary>
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
