using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Tests.Support;
using MediatR;

namespace Crm.BuildingBlocks.Tests.Application.Messaging;

public sealed class DomainEventNotificationTests
{
    [Fact]
    public void ItWrapsTheEvent()
    {
        var domainEvent = new SampleDomainEvent(Clock.Now);

        var notification = new DomainEventNotification<SampleDomainEvent>(domainEvent);

        Assert.Same(domainEvent, notification.DomainEvent);
    }

    [Fact]
    public void NotificationsWrappingEqualEventsAreEqual()
    {
        Assert.Equal(
            new DomainEventNotification<SampleDomainEvent>(new SampleDomainEvent(Clock.Now)),
            new DomainEventNotification<SampleDomainEvent>(new SampleDomainEvent(Clock.Now)));
        Assert.NotEqual(
            new DomainEventNotification<SampleDomainEvent>(new SampleDomainEvent(Clock.Now)),
            new DomainEventNotification<SampleDomainEvent>(new SampleDomainEvent(Clock.Now.AddHours(1))));
    }

    [Fact]
    public void ItIsAMediatRNotification()
    {
        Assert.IsAssignableFrom<INotification>(new DomainEventNotification<SampleDomainEvent>(new SampleDomainEvent(Clock.Now)));
    }
}
