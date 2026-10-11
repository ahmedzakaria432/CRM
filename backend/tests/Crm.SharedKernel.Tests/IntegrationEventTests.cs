namespace Crm.SharedKernel.Tests;

public sealed class IntegrationEventTests
{
    private static readonly Guid EventId = new("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset At = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConcreteRecordExposesIdAndOccurrence()
    {
        var integrationEvent = new SomethingHappened(EventId, At, "x");

        Assert.Equal(EventId, integrationEvent.Id);
        Assert.Equal(At, integrationEvent.OccurredOnUtc);
        Assert.Equal("x", integrationEvent.Detail);
    }

    [Fact]
    public void RecordsWithTheSameValuesAreEqual()
    {
        Assert.Equal(new SomethingHappened(EventId, At, "x"), new SomethingHappened(EventId, At, "x"));
        Assert.NotEqual(new SomethingHappened(EventId, At, "x"), new SomethingHappened(EventId, At, "y"));
    }

    [Fact]
    public void WithCopiesAndLeavesTheOriginal()
    {
        var original = new SomethingHappened(EventId, At, "x");

        var copy = original with { Detail = "y" };

        Assert.Equal("y", copy.Detail);
        Assert.Equal("x", original.Detail);
        Assert.Equal(original.Id, copy.Id);
    }

    [Fact]
    public void ItImplementsIIntegrationEvent()
    {
        Assert.IsAssignableFrom<IIntegrationEvent>(new SomethingHappened(EventId, At, "x"));
    }

    [Fact]
    public void EventsOfDifferentTypesAreNotEqualEvenWithTheSameIdentity()
    {
        IntegrationEvent first = new SomethingHappened(EventId, At, "x");
        IntegrationEvent second = new SomethingElse(EventId, At);

        Assert.NotEqual(first, second);
    }

    private sealed record SomethingHappened(Guid Id, DateTimeOffset OccurredOnUtc, string Detail)
        : IntegrationEvent(Id, OccurredOnUtc);

    private sealed record SomethingElse(Guid Id, DateTimeOffset OccurredOnUtc) : IntegrationEvent(Id, OccurredOnUtc);
}
