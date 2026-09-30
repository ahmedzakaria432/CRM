using Crm.Modules.Identity.Domain;

namespace Crm.Modules.Identity.Application;

/// <summary>Bound from "Identity:Sessions".</summary>
public sealed record SessionSettings(TimeSpan RefreshTokenLifetime, int MaxFailedSignInAttempts, TimeSpan LockoutWindow)
{
    public LockoutPolicy Lockout => new(MaxFailedSignInAttempts, LockoutWindow);
}
