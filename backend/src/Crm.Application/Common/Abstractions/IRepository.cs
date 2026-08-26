using Crm.Application.Common.Specifications;
using Crm.Domain.Common;

namespace Crm.Application.Common.Abstractions;

/// <summary>
/// Aggregate roots only - there is no repository for a child entity (CLAUDE.md, "Backend", DDD
/// rules). <see cref="Remove"/> is the soft-delete entry point; AuditingSaveChangesInterceptor
/// turns the EF delete into a flag update. Every aggregate root carries a rowversion (Story 02
/// Task 9), so <see cref="SetOriginalRowVersion"/> is on the base interface rather than bolted on
/// per-aggregate: a handler applying an update supplied by a client sets the client's last-seen
/// rowversion as the expected value before saving, which is what turns a stale write into a
/// DbUpdateConcurrencyException instead of a silent overwrite.
/// </summary>
public interface IRepository<TAggregate, in TId>
    where TAggregate : AggregateRoot<TId>, IHasRowVersion
    where TId : notnull
{
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    Task<TAggregate?> FirstOrDefaultAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TAggregate>> ListAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default);

    void Add(TAggregate aggregate);

    void Remove(TAggregate aggregate);

    void SetOriginalRowVersion(TAggregate aggregate, byte[] rowVersion);
}
