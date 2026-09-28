using Crm.SharedKernel;

namespace Crm.BuildingBlocks.Application.Messaging;

/// <summary>The non-generic face the outbox dispatcher calls. Implement the generic interface below instead.</summary>
public interface IIntegrationEventConsumer
{
    Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Lives in the SUBSCRIBING module's Application project and is discovered automatically. Runs in that
/// module's own transaction. Delivery is at least once: the idempotency record is written in the same
/// transaction, so a success is never repeated, but a failure is retried. Work with the module's repositories
/// directly - never send a MediatR command from here (its TransactionBehavior would open a second transaction
/// on the same DbContext).
/// </summary>
public interface IIntegrationEventConsumer<in TEvent> : IIntegrationEventConsumer
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);

    Task IIntegrationEventConsumer.HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        HandleAsync((TEvent)integrationEvent, cancellationToken);
}
