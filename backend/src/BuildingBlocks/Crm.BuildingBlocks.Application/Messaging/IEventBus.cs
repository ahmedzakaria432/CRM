using Crm.SharedKernel;

namespace Crm.BuildingBlocks.Application.Messaging;

/// <summary>
/// Publishes an integration event to other modules. Call from a command handler only: TransactionBehavior
/// writes the event to the module's outbox in the command's own transaction, and discards it if the command
/// fails or rolls back. Publishing from a query or a domain-event handler throws.
/// </summary>
public interface IEventBus
{
    void Publish(IIntegrationEvent integrationEvent);
}

/// <summary>The events published in the current scope and not yet staged in an outbox.</summary>
public interface IPendingIntegrationEvents
{
    bool HasPending { get; }

    IReadOnlyList<IIntegrationEvent> DequeueAll();
}

/// <summary>Scoped: one per request, and one per handler delivery in the outbox dispatcher.</summary>
public sealed class OutboxEventBus : IEventBus, IPendingIntegrationEvents
{
    private readonly List<IIntegrationEvent> _pending = [];

    public bool HasPending => _pending.Count > 0;

    public void Publish(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _pending.Add(integrationEvent);
    }

    public IReadOnlyList<IIntegrationEvent> DequeueAll()
    {
        List<IIntegrationEvent> events = [.. _pending];
        _pending.Clear();
        return events;
    }
}
