using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Domain;

/// <summary>A customer contact's portal sign-in (Story 47). Bound to exactly one contact of one customer, for life.
/// NOT ITenantScoped on purpose (like User): registration and sign-in run before a scope exists. Email is unique
/// globally; one account per contact.</summary>
public sealed class PortalAccount : AuditableEntity<PortalAccountId>, IHasRowVersion
{
    public TenantId TenantId { get; private set; } = TenantId.None;

    public Guid CustomerId { get; private set; }

    public Guid ContactId { get; private set; }

    public EmailAddress Email { get; private set; }

    public DisplayName DisplayName { get; private set; }

    public PreferredLocale PreferredLocale { get; private set; }

    public string PasswordHash { get; private set; }

    public int FailedSignInCount { get; private set; }

    public DateTimeOffset? LockedOutUntilUtc { get; private set; }

    public DateTimeOffset? LastSignInAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    private PortalAccount()
    {
        Email = null!;
        DisplayName = null!;
        PreferredLocale = null!;
        PasswordHash = string.Empty;
    }

    private PortalAccount(
        PortalAccountId id, TenantId tenantId, Guid customerId, Guid contactId, EmailAddress email, DisplayName displayName,
        PreferredLocale preferredLocale, string passwordHash) : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        ContactId = contactId;
        Email = email;
        DisplayName = displayName;
        PreferredLocale = preferredLocale;
        PasswordHash = passwordHash;
    }

    /// <summary>Self-registration (D5) and agent provisioning (D19) both create the account through this factory.</summary>
    public static PortalAccount Create(
        TenantId tenantId,
        Guid customerId,
        Guid contactId,
        EmailAddress email,
        DisplayName displayName,
        PreferredLocale locale,
        string passwordHash,
        PortalAccountId? id = null)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(locale);

        if (tenantId == TenantId.None)
        {
            throw new DomainException(new DomainError("Identity.TenantRequired", "A portal account must belong to a tenant."));
        }

        if (customerId == Guid.Empty || contactId == Guid.Empty)
        {
            throw new DomainException(new DomainError("Identity.PortalContactRequired", "A portal account must belong to a customer contact."));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException(new DomainError("Identity.PasswordHashRequired", "A portal account must have a password."));
        }

        return new PortalAccount(id ?? PortalAccountId.New(), tenantId, customerId, contactId, email, displayName, locale, passwordHash);
    }

    public bool IsLockedOut(DateTimeOffset nowUtc) => LockedOutUntilUtc is { } until && until > nowUtc;

    /// <summary>Ignored while the account is locked (attempts while locked never extend the lock). At the
    /// limit of the policy the account locks for its window and the counter restarts.</summary>
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

    public void UpdateProfile(DisplayName displayName, PreferredLocale locale)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(locale);

        DisplayName = displayName;
        PreferredLocale = locale;
    }

    /// <summary>Self-service: every portal session except <paramref name="keepSessionId"/> is revoked.</summary>
    public void ChangePassword(string newPasswordHash, Guid? keepSessionId, DateTimeOffset nowUtc)
    {
        SetPasswordHash(newPasswordHash);
        Raise(new PortalPasswordChanged(Id, keepSessionId, nowUtc));
    }

    /// <summary>An agent sets the password (D19): clears any lockout and revokes every session.</summary>
    public void SetPasswordByAgent(string newPasswordHash, DateTimeOffset nowUtc)
    {
        SetPasswordHash(newPasswordHash);
        FailedSignInCount = 0;
        LockedOutUntilUtc = null;
        Raise(new PortalPasswordChanged(Id, null, nowUtc));
    }

    private void SetPasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new DomainException(new DomainError("Identity.PasswordHashRequired", "A portal account must have a password."));
        }

        PasswordHash = newPasswordHash;
    }
}
