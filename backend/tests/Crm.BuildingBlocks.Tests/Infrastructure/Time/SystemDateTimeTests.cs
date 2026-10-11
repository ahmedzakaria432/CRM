using Crm.BuildingBlocks.Infrastructure.Time;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Time;

public sealed class SystemDateTimeTests
{
    [Fact]
    public void TheClockIsAlwaysUtc()
    {
        Assert.Equal(TimeSpan.Zero, new SystemDateTime().UtcNow.Offset);
    }

    [Fact]
    public void TheClockIsAfterTheUnixEpoch()
    {
        Assert.True(new SystemDateTime().UtcNow > DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void TwoReadsNeverGoBackwards()
    {
        var clock = new SystemDateTime();

        var first = clock.UtcNow;
        var second = clock.UtcNow;

        Assert.True(second >= first);
    }
}
