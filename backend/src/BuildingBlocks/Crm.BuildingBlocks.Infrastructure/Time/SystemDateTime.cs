using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.BuildingBlocks.Infrastructure.Time;

/// <summary>The one production <see cref="IDateTime"/>. Crm.BuildingBlocks.Infrastructure is the only
/// project where System.TimeProvider is not banned (Directory.Build.props), so this is the single
/// place the system clock is read.</summary>
public sealed class SystemDateTime : IDateTime
{
    public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();
}
