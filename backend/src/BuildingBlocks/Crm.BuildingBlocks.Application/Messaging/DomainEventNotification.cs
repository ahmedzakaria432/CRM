using Crm.SharedKernel;
using MediatR;

namespace Crm.BuildingBlocks.Application.Messaging;

/// <summary>
/// Wraps a domain event as a MediatR notification. Domain projects must not reference MediatR
/// (BannedSymbols.txt bans the namespace there), so this adapter is what lets
/// IDomainEventDispatcher publish through MediatR's IPublisher without the domain event type
/// itself implementing INotification.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
