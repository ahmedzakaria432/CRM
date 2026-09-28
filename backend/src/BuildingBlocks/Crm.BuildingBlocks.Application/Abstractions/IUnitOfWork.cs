using Crm.SharedKernel;

namespace Crm.BuildingBlocks.Application.Abstractions;

/// <summary>
/// TransactionBehavior lives in Crm.BuildingBlocks.Application and must not reference EF Core
/// directly (no Application project carries an EF Core package). This abstraction is the seam:
/// everything TransactionBehavior needs from the persistence layer - opening/closing a transaction,
/// harvesting domain events, saving - goes through here.
/// </summary>
/// <remarks>
/// There is one unit of work PER MODULE, never one for the whole application: each module declares
/// its own interface deriving from this one (e.g. <c>ISampleUnitOfWork</c>) in its Application
/// project, and its DbContext - a <c>ModuleDbContext</c> - is the only implementation. Handlers
/// inject their module's interface, never this one. TransactionBehavior finds the right one through
/// <see cref="Modules.IUnitOfWorkResolver"/>.
/// </remarks>
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
