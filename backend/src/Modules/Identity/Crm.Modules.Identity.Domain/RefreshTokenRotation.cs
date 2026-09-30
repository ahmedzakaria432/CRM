namespace Crm.Modules.Identity.Domain;

public enum RefreshTokenRotation
{
    Rotated,
    ReuseDetected,
    Expired,
    Revoked,
}
