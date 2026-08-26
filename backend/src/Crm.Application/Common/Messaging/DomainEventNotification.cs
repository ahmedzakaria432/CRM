using Crm.Domain.Common;
using MediatR;

namespace Crm.Application.Common.Messaging;

/// <summary>
/// Wraps a domain event as a MediatR notification. Crm.Domain must not reference MediatR
/// (BannedSymbols.txt bans the namespace there), so this adapter is what lets
/// IDomainEventDispatcher publish through MediatR's IPublisher without the domain event type
/// itself implementing INotification.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
