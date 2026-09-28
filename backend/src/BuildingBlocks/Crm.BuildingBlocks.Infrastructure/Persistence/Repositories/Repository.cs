using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic repository over the DbContext of the module that owns <typeparamref name="TAggregate"/>.
/// Registered per aggregate with <c>AddRepository&lt;TAggregate, TId, TContext&gt;()</c> - there is no
/// open-generic registration, because with one DbContext per module the container cannot guess
/// which context an aggregate lives in.
/// </summary>
public class Repository<TAggregate, TId, TContext>(TContext context) : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>, IHasRowVersion
    where TId : notnull
    where TContext : ModuleDbContext
{
    protected TContext Context { get; } = context;

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
