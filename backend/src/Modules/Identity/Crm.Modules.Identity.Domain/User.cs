using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Domain;

/// <summary>A staff user. NOT ITenantScoped on purpose: sign-in runs before a scope exists and must read and
/// write the user (failed-attempt counter), which the scope filter and write guard would refuse. TenantId is
/// therefore a plain column, and the admin queries compare it to the caller's tenant explicitly.</summary>
public sealed class User : AuditableEntity<UserId>, IHasRowVersion
{
    public TenantId TenantId { get; private set; } = TenantId.None;

    public EmailAddress Email { get; private set; }

    public DisplayName DisplayName { get; private set; }

    public PreferredLocale PreferredLocale { get; private set; }

    public string PasswordHash { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int FailedSignInCount { get; private set; }

    public DateTimeOffset? LockedOutUntilUtc { get; private set; }

    public DateTimeOffset? LastSignInAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    private User()
    {
        Email = null!;
        DisplayName = null!;
        PreferredLocale = null!;
        PasswordHash = string.Empty;
    }

    private User(UserId id, TenantId tenantId, EmailAddress email, DisplayName displayName, PreferredLocale preferredLocale, string passwordHash)
        : base(id)
    {
        TenantId = tenantId;
        Email = email;
        DisplayName = displayName;
        PreferredLocale = preferredLocale;
        PasswordHash = passwordHash;
    }

    public static User Create(
        TenantId tenantId,
        EmailAddress email,
        DisplayName displayName,
        PreferredLocale locale,
        string passwordHash,
        UserId? id = null)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(locale);

        if (tenantId == TenantId.None)
        {
            throw new DomainException(new DomainError("Identity.TenantRequired", "A user must belong to a tenant."));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException(new DomainError("Identity.PasswordHashRequired", "A user must have a password."));
        }

        return new User(id ?? UserId.New(), tenantId, email, displayName, locale, passwordHash);
    }

    public bool IsLockedOut(DateTimeOffset nowUtc) => LockedOutUntilUtc is { } until && until > nowUtc;

    /// <summary>Ignored while the account is locked (attempts while locked never extend the lock). At the
    /// policy's limit the account locks for the window and the counter restarts.</summary>
    public void RecordFailedSignIn(DateTimeOffset nowUtc, LockoutPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (IsLockedOut(nowUtc))
        {
            return;
        }

        FailedSignInCount++;

        if (FailedSignInCount >= policy.MaxFailedAttempts)
        {
            LockedOutUntilUtc = nowUtc + policy.Window;
            FailedSignInCount = 0;
        }
    }

    public void RecordSuccessfulSignIn(DateTimeOffset nowUtc)
    {
        FailedSignInCount = 0;
        LockedOutUntilUtc = null;
        LastSignInAtUtc = nowUtc;
    }

    public void Rename(DisplayName displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        DisplayName = displayName;
    }

    public void ChangePreferredLocale(PreferredLocale locale)
    {
        ArgumentNullException.ThrowIfNull(locale);
        PreferredLocale = locale;
    }

    /// <summary>Self-service change: every session except <paramref name="keepSessionId"/> is revoked.</summary>
    public void ChangePassword(string newPasswordHash, Guid? keepSessionId, DateTimeOffset nowUtc)
    {
        SetPasswordHash(newPasswordHash);
        Raise(new UserPasswordChanged(Id, keepSessionId, nowUtc));
    }

    /// <summary>Administrator reset: clears any lockout and revokes every session.</summary>
    public void ResetPassword(string newPasswordHash, DateTimeOffset nowUtc)
    {
        SetPasswordHash(newPasswordHash);
        FailedSignInCount = 0;
        LockedOutUntilUtc = null;
        Raise(new UserPasswordChanged(Id, null, nowUtc));
    }

    public void Deactivate(DateTimeOffset nowUtc)
    {
        if (!IsActive)
        {
            throw new DomainException(new DomainError("Identity.UserAlreadyInactive", "The user is already inactive."));
        }

        IsActive = false;
        Raise(new UserDeactivated(Id, nowUtc));
    }

    public void Reactivate()
    {
        if (IsActive)
        {
            throw new DomainException(new DomainError("Identity.UserAlreadyActive", "The user is already active."));
        }

        IsActive = true;
        FailedSignInCount = 0;
        LockedOutUntilUtc = null;
    }

    private void SetPasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new DomainException(new DomainError("Identity.PasswordHashRequired", "A user must have a password."));
        }

        PasswordHash = newPasswordHash;
    }
}
