namespace Crm.Modules.Identity.Domain;

public enum SessionRevocationReason
{
    SignedOut,
    ReuseDetected,
    UserDeactivated,
    PasswordChanged,
    PasswordReset,
}
