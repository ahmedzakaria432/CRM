using Crm.BuildingBlocks.Infrastructure.Outbox;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Outbox;

public sealed class OutboxSettingsTests
{
    [Fact]
    public void TheDefaultPollsEveryFiveSecondsInBatchesOfTwentyWithFiveAttempts()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), OutboxSettings.Default.PollingInterval);
        Assert.Equal(20, OutboxSettings.Default.BatchSize);
        Assert.Equal(5, OutboxSettings.Default.MaxAttempts);
    }

    [Fact]
    public void SettingsHaveValueEquality()
    {
        Assert.Equal(new OutboxSettings(TimeSpan.FromSeconds(1), 2, 3), new OutboxSettings(TimeSpan.FromSeconds(1), 2, 3));
        Assert.NotEqual(new OutboxSettings(TimeSpan.FromSeconds(1), 2, 3), new OutboxSettings(TimeSpan.FromSeconds(1), 2, 4));
    }
}
