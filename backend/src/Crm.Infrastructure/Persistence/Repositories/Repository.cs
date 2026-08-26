using Crm.Application.Common.Abstractions;
using Crm.Application.Common.Specifications;
using Crm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

public class Repository<TAggregate, TId>(CrmDbContext context) : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, IHasRowVersion
    where TId : notnull
{
    protected CrmDbContext Context { get; } = context;

    public Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default) =>
        Context.Set<TAggregate>().FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken);

    public Task<TAggregate?> FirstOrDefaultAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default) =>
        ApplySpecification(specification).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TAggregate>> ListAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default) =>
        await ApplySpecification(specification).ToListAsync(cancellationToken);

    public Task<int> CountAsync(Specification<TAggregate> specification, CancellationToken cancellationToken = default) =>
        ApplySpecification(specification).CountAsync(cancellationToken);

    public void Add(TAggregate aggregate) => Context.Set<TAggregate>().Add(aggregate);

    public void Remove(TAggregate aggregate) => Context.Set<TAggregate>().Remove(aggregate);

    public void SetOriginalRowVersion(TAggregate aggregate, byte[] rowVersion) =>
        Context.Entry(aggregate).Property(nameof(IHasRowVersion.RowVersion)).OriginalValue = rowVersion;

    private IQueryable<TAggregate> ApplySpecification(Specification<TAggregate> specification) =>
        SpecificationEvaluator.GetQuery(Context.Set<TAggregate>(), specification);
}
