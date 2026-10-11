using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel;

namespace Crm.BuildingBlocks.Tests.Application.Messaging;

public sealed class IntegrationEventConsumerRegistryTests
{
    private static IntegrationEventConsumerRegistry ScanTestAssembly() =>
        IntegrationEventConsumerRegistry.Scan([typeof(SampleConsumer).Assembly]);

    [Fact]
    public void ScanFindsAConsumerByItsEventTypeName()
    {
        var registry = ScanTestAssembly();

        var found = registry.TryGet(typeof(SampleIntegrationEvent).FullName ?? string.Empty, out var subscription);

        Assert.True(found);
        Assert.NotNull(subscription);
        Assert.Equal(typeof(SampleIntegrationEvent), subscription.EventType);
        Assert.Contains(typeof(SampleConsumer), subscription.HandlerTypes);
    }

    [Fact]
    public void TwoConsumersOfOneEventAreBothListed()
    {
        var registry = ScanTestAssembly();

        registry.TryGet(typeof(MixedIntegrationEvent).FullName ?? string.Empty, out var subscription);

        Assert.NotNull(subscription);
        Assert.Equal(2, subscription.HandlerTypes.Count);
        Assert.Contains(typeof(MixedOkConsumer), subscription.HandlerTypes);
        Assert.Contains(typeof(MixedFailingConsumer), subscription.HandlerTypes);
    }

    [Fact]
    public void AClassImplementingTwoEventInterfacesAppearsUnderBoth()
    {
        var registry = ScanTestAssembly();

        registry.TryGet(typeof(SecondIntegrationEvent).FullName ?? string.Empty, out var second);
        registry.TryGet(typeof(ThirdIntegrationEvent).FullName ?? string.Empty, out var third);

        Assert.NotNull(second);
        Assert.NotNull(third);
        Assert.Contains(typeof(DualConsumer), second.HandlerTypes);
        Assert.Contains(typeof(DualConsumer), third.HandlerTypes);
    }

    [Fact]
    public void AbstractClassesAreSkipped()
    {
        var registry = ScanTestAssembly();

        registry.TryGet(typeof(SampleIntegrationEvent).FullName ?? string.Empty, out var subscription);

        Assert.NotNull(subscription);
        Assert.DoesNotContain(typeof(AbstractConsumer), subscription.HandlerTypes);
        Assert.DoesNotContain(typeof(AbstractConsumer), registry.HandlerTypes);
    }

    [Fact]
    public void AnEventNobodyConsumesIsNotFound()
    {
        var registry = ScanTestAssembly();

        Assert.False(registry.TryGet(typeof(NoSubscriberIntegrationEvent).FullName ?? string.Empty, out var subscription));
        Assert.Null(subscription);
    }

    [Fact]
    public void AnUnknownNameIsNotFound()
    {
        Assert.False(ScanTestAssembly().TryGet("No.Such.Event", out _));
    }

    [Fact]
    public void LookupIsOrdinal()
    {
        var name = (typeof(SampleIntegrationEvent).FullName ?? string.Empty).ToUpperInvariant();

        Assert.False(ScanTestAssembly().TryGet(name, out _));
    }

    [Fact]
    public void HandlerTypesIsTheDistinctUnionOfAllHandlers()
    {
        var handlers = ScanTestAssembly().HandlerTypes;

        Assert.Equal(handlers.Count, handlers.Distinct().Count());
        Assert.Contains(typeof(SampleConsumer), handlers);
        Assert.Contains(typeof(FailingConsumer), handlers);
        Assert.Contains(typeof(PublishingConsumer), handlers);
        Assert.Single(handlers, h => h == typeof(DualConsumer));
    }

    [Fact]
    public void ScanningNoAssembliesYieldsAnEmptyRegistry()
    {
        var registry = IntegrationEventConsumerRegistry.Scan([]);

        Assert.Empty(registry.HandlerTypes);
        Assert.False(registry.TryGet(typeof(SampleIntegrationEvent).FullName ?? string.Empty, out _));
    }

    [Fact]
    public async Task TheNonGenericHandleForwardsToTheTypedHandler()
    {
        var probe = new ConsumerProbe();
        var consumer = Assert.IsAssignableFrom<IIntegrationEventConsumer>(new SampleConsumer(probe, Substitute.For<Crm.BuildingBlocks.Application.Abstractions.ICorrelationIdAccessor>()));
        var integrationEvent = new SampleIntegrationEvent(Guid.NewGuid(), Clock.Now, "payload");

        await consumer.HandleAsync(integrationEvent, TestContext.Current.CancellationToken);

        var handled = Assert.Single(probe.Handled);
        Assert.Same(integrationEvent, handled.Event);
    }

    [Fact]
    public async Task TheNonGenericHandleRejectsAnEventOfAnotherType()
    {
        var consumer = Assert.IsAssignableFrom<IIntegrationEventConsumer>(new SampleConsumer(
            new ConsumerProbe(), Substitute.For<Crm.BuildingBlocks.Application.Abstractions.ICorrelationIdAccessor>()));
        IIntegrationEvent wrong = new SecondIntegrationEvent(Guid.NewGuid(), Clock.Now);

        await Assert.ThrowsAsync<InvalidCastException>(
            () => consumer.HandleAsync(wrong, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SubscriptionRecordsHaveValueEquality()
    {
        var handlers = new List<Type> { typeof(SampleConsumer) };

        Assert.Equal(
            new IntegrationEventSubscription(typeof(SampleIntegrationEvent), handlers),
            new IntegrationEventSubscription(typeof(SampleIntegrationEvent), handlers));
    }
}
