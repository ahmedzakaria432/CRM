namespace Crm.BuildingBlocks.Application.Abstractions;

/// <summary>
/// The only way to read the clock. Inject this wherever a timestamp is needed - handlers, the
/// auditing interceptor, background jobs. <c>System.DateTime</c> is banned outright and
/// <c>DateTimeOffset.UtcNow</c>/<c>Now</c> and <c>System.TimeProvider</c> are banned everywhere
/// except the one implementation (build/BannedSymbols.*.txt), so the clock stays replaceable.
/// Domain methods never read it themselves: they take the timestamp as a parameter.
/// </summary>
public interface IDateTime
{
    /// <summary>The current instant, always UTC (offset zero).</summary>
    DateTimeOffset UtcNow { get; }
}
