namespace Crm.SharedKernel;

/// <summary>
/// "This happened" between modules. Records live in the PUBLISHING module's Contracts project, carry
/// primitives only, and are append-only: add properties, never rename or remove one (or the type) - an
/// undelivered outbox row still holds the old shape and type name.
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }

    DateTimeOffset OccurredOnUtc { get; }
}

public abstract record IntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc) : IIntegrationEvent;
