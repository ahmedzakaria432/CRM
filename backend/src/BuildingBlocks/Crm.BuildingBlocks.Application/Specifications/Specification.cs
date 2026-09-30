using System.Linq.Expressions;

namespace Crm.BuildingBlocks.Application.Specifications;

/// <summary>
/// Reusable query intent (CLAUDE.md, "Backend", DDD rules - e.g. OverdueTicketsSpecification).
/// Two named escape hatches, each lifting exactly one filter: <see cref="IgnoreSoftDeleteFilter"/> lifts the
/// named soft-delete filter (Infrastructure/SpecificationEvaluator); <see cref="IgnoreOrganisationScopeFilter"/>
/// lifts the named organisation-scope filter (Story 06) and is used only by the sign-in membership lookup.
/// Neither ever lifts every filter, so a specification can never accidentally leak another tenant's rows.
/// </summary>
public abstract class Specification<T>
{
    public Expression<Func<T, bool>>? Criteria { get; private set; }

    public List<Expression<Func<T, object>>> Includes { get; } = [];

    public Expression<Func<T, object>>? OrderBy { get; private set; }

    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public Expression<Func<T, object>>? ThenBy { get; private set; }

    public int? Skip { get; private set; }

    public int? Take { get; private set; }

    public bool AsNoTracking { get; private set; }

    public bool IgnoreSoftDeleteFilter { get; private set; }

    public bool IgnoreOrganisationScopeFilter { get; private set; }

    protected void Where(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    protected void AddInclude(Expression<Func<T, object>> include) => Includes.Add(include);

    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescending) => OrderByDescending = orderByDescending;

    /// <summary>Secondary ascending key after OrderBy/OrderByDescending - paging on a non-unique key is non-deterministic on SQL Server without one.</summary>
    protected void ApplyThenBy(Expression<Func<T, object>> thenBy) => ThenBy = thenBy;

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }

    protected void AsReadOnly() => AsNoTracking = true;

    protected void IncludeSoftDeleted() => IgnoreSoftDeleteFilter = true;

    /// <summary>THE ONE PERMITTED SCOPE LIFT (Story 06): used only by the sign-in membership lookup
    /// (Organisation's MembershipsOfUserSpecification). Lifts only ModuleDbContext.ScopeFilterName - the
    /// soft-delete filter stays. Adding a second caller is a security review, not a refactor.</summary>
    protected void IgnoreOrganisationScope() => IgnoreOrganisationScopeFilter = true;
}
