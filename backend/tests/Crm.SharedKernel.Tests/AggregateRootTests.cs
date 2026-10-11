namespace Crm.SharedKernel.Tests;

public sealed class AggregateRootTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ANewRootHasNoEvents()
    {
        Assert.Empty(new Order(Guid.NewGuid()).DomainEvents);
    }

    [Fact]
    public void RaisedEventsAreRecordedInOrder()
    {
        var order = new Order(Guid.NewGuid());
        var first = new OrderPlaced(At);
        var second = new OrderPlaced(At.AddMinutes(1));

        order.Place(first);
        order.Place(second);

        Assert.Collection(
            order.DomainEvents,
            e => Assert.Same(first, e),
            e => Assert.Same(second, e));
    }

    [Fact]
    public void DomainEventsIsAReadOnlyView()
    {
        var order = new Order(Guid.NewGuid());
        order.Place(new OrderPlaced(At));

        var view = Assert.IsAssignableFrom<ICollection<IDomainEvent>>(order.DomainEvents);

        Assert.True(view.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => view.Clear());
    }

    [Fact]
    public void ClearDomainEventsEmptiesTheList()
    {
        var order = new Order(Guid.NewGuid());
        order.Place(new OrderPlaced(At));

        order.ClearDomainEvents();

        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void ClearingTwiceIsSafe()
    {
        var order = new Order(Guid.NewGuid());
        order.Place(new OrderPlaced(At));

        order.ClearDomainEvents();
        order.ClearDomainEvents();

        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void EventsCanBeRaisedAgainAfterClearing()
    {
        var order = new Order(Guid.NewGuid());
        order.Place(new OrderPlaced(At));
        order.ClearDomainEvents();

        order.Place(new OrderPlaced(At));

        Assert.Single(order.DomainEvents);
    }

    [Fact]
    public void ARootImplementsIHasDomainEvents()
    {
        Assert.IsAssignableFrom<IHasDomainEvents>(new Order(Guid.NewGuid()));
    }

    [Fact]
    public void ARootIsAnEntityWithEqualityById()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new Order(id), new Order(id));
    }

    private sealed record OrderPlaced(DateTimeOffset OccurredOnUtc) : IDomainEvent;

    private sealed class Order(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Place(IDomainEvent domainEvent) => Raise(domainEvent);
    }
}
