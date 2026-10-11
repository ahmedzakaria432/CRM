using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel;
using MediatR;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Persistence;

public sealed class DomainEventDispatcherTests
{
    private static (IPublisher Publisher, List<INotification> Published) Recorder()
    {
        var publisher = Substitute.For<IPublisher>();
        List<INotification> published = [];
        publisher
            .When(p => p.Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>()))
            .Do(call => published.Add(call.Arg<INotification>()));
        return (publisher, published);
    }

    [Fact]
    public async Task EachEventIsPublishedAsANotificationOfItsRuntimeTypeInOrder()
    {
        var (publisher, published) = Recorder();
        var first = new SampleDomainEvent(Clock.Now);
        var second = new SecondDomainEvent(Clock.Now);

        await new DomainEventDispatcher(publisher).DispatchAsync([first, second], TestContext.Current.CancellationToken);

        Assert.Equal(2, published.Count);
        Assert.Equal(typeof(DomainEventNotification<SampleDomainEvent>), published[0].GetType());
        Assert.Equal(typeof(DomainEventNotification<SecondDomainEvent>), published[1].GetType());
        Assert.Same(first, Assert.IsType<DomainEventNotification<SampleDomainEvent>>(published[0]).DomainEvent);
        Assert.Same(second, Assert.IsType<DomainEventNotification<SecondDomainEvent>>(published[1]).DomainEvent);
    }

    [Fact]
    public async Task TheCancellationTokenIsForwarded()
    {
        var publisher = Substitute.For<IPublisher>();
        using var source = new CancellationTokenSource();

        await new DomainEventDispatcher(publisher).DispatchAsync([new SampleDomainEvent(Clock.Now)], source.Token);

        await publisher.Received(1).Publish(Arg.Any<INotification>(), source.Token);
    }

    [Fact]
    public async Task NoEventsPublishNothing()
    {
        var publisher = Substitute.For<IPublisher>();

        await new DomainEventDispatcher(publisher).DispatchAsync([], TestContext.Current.CancellationToken);

        await publisher.DidNotReceiveWithAnyArgs().Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task APublisherFailurePropagatesAndLaterEventsAreNotPublished()
    {
        var publisher = Substitute.For<IPublisher>();
        var calls = 0;
        publisher
            .Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls++;
                return Task.FromException(new InvalidOperationException("handler failed"));
            });
        IReadOnlyCollection<IDomainEvent> events = [new SampleDomainEvent(Clock.Now), new SecondDomainEvent(Clock.Now)];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new DomainEventDispatcher(publisher).DispatchAsync(events, TestContext.Current.CancellationToken));

        Assert.Equal("handler failed", exception.Message);
        Assert.Equal(1, calls);
    }
}
