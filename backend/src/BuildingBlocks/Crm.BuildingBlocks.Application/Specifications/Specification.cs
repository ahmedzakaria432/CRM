using System.Linq.Expressions;

namespace Crm.BuildingBlocks.Application.Specifications;

/// <summary>
/// Reusable query intent (CLAUDE.md, "Backend", DDD rules - e.g. OverdueTicketsSpecification).
/// <see cref="IgnoreSoftDeleteFilter"/> is the only escape hatch a specification exposes: it lifts
/// exactly the named soft-delete filter (Infrastructure/SpecificationEvaluator), never every filter,
/// so a specification can never accidentally leak another tenant's rows once story 06 adds the
/// tenant filter.
/// </summary>
public abstract class Specification<T>
{
    public Expression<Func<T, bool>>? Criteria { get; private set; }

    public List<Expression<Func<T, object>>> Includes { get; } = [];

    public Expression<Func<T, object>>? OrderBy { get; private set; }

    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public int? Skip { get; private set; }

    public int? Take { get; private set; }

    public bool AsNoTracking { get; private set; }

    public bool IgnoreSoftDeleteFilter { get; private set; }

    protected void Where(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    protected void AddInclude(Expression<Func<T, object>> include) => Includes.Add(include);

    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescending) => OrderByDescending = orderByDescending;

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }

    protected void AsReadOnly() => AsNoTracking = true;

    protected void IncludeSoftDeleted() => IgnoreSoftDeleteFilter = true;
}
