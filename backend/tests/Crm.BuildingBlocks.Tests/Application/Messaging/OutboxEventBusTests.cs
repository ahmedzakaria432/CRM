using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel;

namespace Crm.BuildingBlocks.Tests.Application.Messaging;

public sealed class OutboxEventBusTests
{
    private static SampleIntegrationEvent Event(string payload) => new(Guid.NewGuid(), Clock.Now, payload);

    [Fact]
    public void ANewBusHasNothingPending()
    {
        Assert.False(new OutboxEventBus().HasPending);
    }

    [Fact]
    public void PublishMakesAnEventPending()
    {
        var bus = new OutboxEventBus();

        bus.Publish(Event("a"));

        Assert.True(bus.HasPending);
    }

    [Fact]
    public void DequeueAllReturnsEventsInPublishOrderAndEmptiesTheBus()
    {
        var bus = new OutboxEventBus();
        var first = Event("a");
        var second = Event("b");
        bus.Publish(first);
        bus.Publish(second);

        var events = bus.DequeueAll();

        Assert.Equal([first, second], events);
        Assert.False(bus.HasPending);
    }

    [Fact]
    public void ASecondDequeueIsEmpty()
    {
        var bus = new OutboxEventBus();
        bus.Publish(Event("a"));
        bus.DequeueAll();

        Assert.Empty(bus.DequeueAll());
    }

    [Fact]
    public void TheReturnedListIsACopy()
    {
        var bus = new OutboxEventBus();
        bus.Publish(Event("a"));

        var events = bus.DequeueAll();
        bus.Publish(Event("b"));

        Assert.Single(events);
        Assert.Single(bus.DequeueAll());
    }

    [Fact]
    public void PublishRejectsANullEvent()
    {
        var bus = new OutboxEventBus();

        NullArguments.AssertThrowsArgumentNull(bus.Publish, new object?[] { null });
    }

    [Fact]
    public void OneInstanceServesBothInterfaces()
    {
        var bus = new OutboxEventBus();
        var publisher = Assert.IsAssignableFrom<IEventBus>(bus);
        var pending = Assert.IsAssignableFrom<IPendingIntegrationEvents>(bus);

        publisher.Publish(Event("a"));

        Assert.True(pending.HasPending);
        Assert.Single(pending.DequeueAll());
    }

    [Fact]
    public void AnyIntegrationEventImplementationCanBePublished()
    {
        var bus = new OutboxEventBus();
        IIntegrationEvent integrationEvent = new SecondIntegrationEvent(Guid.NewGuid(), Clock.Now);

        bus.Publish(integrationEvent);

        Assert.Same(integrationEvent, Assert.Single(bus.DequeueAll()));
    }
}
