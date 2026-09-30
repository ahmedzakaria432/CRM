namespace Crm.Modules.Identity.Domain;

/// <summary>Consecutive failed sign-ins that lock an account, and for how long. Bound from configuration, so an
/// invalid value is a configuration error, not a domain rule.</summary>
public sealed record LockoutPolicy
{
    public LockoutPolicy(int maxFailedAttempts, TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFailedAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        MaxFailedAttempts = maxFailedAttempts;
        Window = window;
    }

    public int MaxFailedAttempts { get; }

    public TimeSpan Window { get; }
}
