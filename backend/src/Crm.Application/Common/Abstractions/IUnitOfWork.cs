using Crm.Domain.Common;

namespace Crm.Application.Common.Abstractions;

/// <summary>
/// TransactionBehavior lives in Crm.Application and must not reference EF Core directly (Story 02
/// Task 1 - Crm.Application carries no EF Core package). This abstraction is the seam: everything
/// TransactionBehavior needs from the persistence layer - opening/closing a transaction, harvesting
/// domain events, saving - goes through here, and Crm.Infrastructure's CrmDbContext is the only
/// implementation.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Harvests every domain event raised by a tracked aggregate and clears them from the
    /// aggregate in the same call. Must be read before CommitTransactionAsync - a handler that
    /// calls SaveChangesAsync and later clears the change tracker would otherwise lose them.</summary>
    IReadOnlyCollection<IDomainEvent> GetAndClearDomainEvents();
}
