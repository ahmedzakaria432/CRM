using System.Collections.Concurrent;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.SharedKernel;

namespace Crm.BuildingBlocks.Tests.Support;

public sealed record SampleIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc, string Payload)
    : IntegrationEvent(Id, OccurredOnUtc);

public sealed record SecondIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc) : IntegrationEvent(Id, OccurredOnUtc);

public sealed record FailingIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc) : IntegrationEvent(Id, OccurredOnUtc);

public sealed record MixedIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc) : IntegrationEvent(Id, OccurredOnUtc);

public sealed record PublishingIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc) : IntegrationEvent(Id, OccurredOnUtc);

public sealed record NoSubscriberIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc) : IntegrationEvent(Id, OccurredOnUtc);

public sealed record ThirdIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc) : IntegrationEvent(Id, OccurredOnUtc);

public sealed record SampleDomainEvent(DateTimeOffset OccurredOnUtc) : IDomainEvent;

/// <summary>What the consumer fixtures saw. Registered as a singleton by the dispatcher harness.</summary>
public sealed class ConsumerProbe
{
    public ConcurrentQueue<(string Handler, IIntegrationEvent Event, string? CorrelationId)> Handled { get; } = new();

    public int Count(string handler) => Handled.Count(h => h.Handler == handler);
}

public sealed class SampleConsumer(ConsumerProbe probe, ICorrelationIdAccessor correlation)
    : IIntegrationEventConsumer<SampleIntegrationEvent>
{
    public Task HandleAsync(SampleIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        probe.Handled.Enqueue((nameof(SampleConsumer), integrationEvent, correlation.CorrelationId));
        return Task.CompletedTask;
    }
}

public sealed class FailingConsumer : IIntegrationEventConsumer<FailingIntegrationEvent>
{
    public Task HandleAsync(FailingIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom");
}

public sealed class MixedOkConsumer(ConsumerProbe probe) : IIntegrationEventConsumer<MixedIntegrationEvent>
{
    public Task HandleAsync(MixedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        probe.Handled.Enqueue((nameof(MixedOkConsumer), integrationEvent, null));
        return Task.CompletedTask;
    }
}

public sealed class MixedFailingConsumer : IIntegrationEventConsumer<MixedIntegrationEvent>
{
    public Task HandleAsync(MixedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("mixed boom");
}

public sealed class PublishingConsumer(IEventBus bus) : IIntegrationEventConsumer<PublishingIntegrationEvent>
{
    public Task HandleAsync(PublishingIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        bus.Publish(new SecondIntegrationEvent(Guid.NewGuid(), integrationEvent.OccurredOnUtc));
        return Task.CompletedTask;
    }
}

/// <summary>Implements two event interfaces, so the registry lists it under both event names.</summary>
public sealed class DualConsumer
    : IIntegrationEventConsumer<SecondIntegrationEvent>, IIntegrationEventConsumer<ThirdIntegrationEvent>
{
    Task IIntegrationEventConsumer<SecondIntegrationEvent>.HandleAsync(
        SecondIntegrationEvent integrationEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    Task IIntegrationEventConsumer<ThirdIntegrationEvent>.HandleAsync(
        ThirdIntegrationEvent integrationEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    Task IIntegrationEventConsumer.HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

public abstract class AbstractConsumer : IIntegrationEventConsumer<SampleIntegrationEvent>
{
    public abstract Task HandleAsync(SampleIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}

public sealed record SecondDomainEvent(DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record DomainEventTriggerIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc) : IntegrationEvent(Id, OccurredOnUtc);

/// <summary>Adds an aggregate that raised a domain event to the subscriber's own context, like a real consumer would.</summary>
public sealed class DomainEventRaisingConsumer(Persistence.TestDbContext database, IDateTime clock)
    : IIntegrationEventConsumer<DomainEventTriggerIntegrationEvent>
{
    public Task HandleAsync(DomainEventTriggerIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var row = Persistence.EventfulRow.Create(Guid.NewGuid());
        row.Touch(clock.UtcNow);
        database.EventfulRows.Add(row);
        return Task.CompletedTask;
    }
}
