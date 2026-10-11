using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.Modules.Tickets.Tests.Support;

/// <summary>A fixed instant and an <see cref="IDateTime"/> substitute that returns it. Tests never read the real clock.</summary>
public static class Clock
{
    public static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset Later(int minutes) => Now.AddMinutes(minutes);

    public static IDateTime Fixed(DateTimeOffset? at = null)
    {
        var clock = Substitute.For<IDateTime>();
        clock.UtcNow.Returns(at ?? Now);
        return clock;
    }
}
