using Crm.Domain.Common;

namespace Crm.Application.Common.Abstractions;

/// <summary>
/// Dispatches domain events harvested by TransactionBehavior, after the transaction that produced
/// them has committed. Crm.Domain must not reference MediatR (BannedSymbols.txt), so the
/// Crm.Infrastructure implementation wraps each event in a notification before publishing it.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
