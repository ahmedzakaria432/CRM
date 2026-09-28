using Crm.SharedKernel;

namespace Crm.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Dispatches domain events harvested by TransactionBehavior, after the transaction that produced
/// them has committed. Domain projects must not reference MediatR (BannedSymbols.Pure.txt), so the
/// Crm.BuildingBlocks.Infrastructure implementation wraps each event in a notification before publishing it.
/// Domain events never leave their module: a handler for one lives in the same module that raised it.
/// Another module learns about the change only through an integration event in the raising module's Contracts.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
