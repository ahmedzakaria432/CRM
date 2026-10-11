using System.Linq.Expressions;
using Crm.BuildingBlocks.Application.Specifications;

namespace Crm.BuildingBlocks.Tests.Application.Specifications;

public sealed class SpecificationTests
{
    [Fact]
    public void ANewSpecificationHasNoCriteriaOrOrderingOrPaging()
    {
        var specification = new WidgetSpecification();

        Assert.Null(specification.Criteria);
        Assert.Empty(specification.Includes);
        Assert.Null(specification.OrderBy);
        Assert.Null(specification.OrderByDescending);
        Assert.Null(specification.ThenBy);
        Assert.Null(specification.Skip);
        Assert.Null(specification.Take);
        Assert.False(specification.AsNoTracking);
        Assert.False(specification.IgnoreSoftDeleteFilter);
        Assert.False(specification.IgnoreOrganisationScopeFilter);
    }

    [Fact]
    public void WhereSetsTheCriteria()
    {
        var specification = new WidgetSpecification();
        Expression<Func<Widget, bool>> criteria = w => w.Id > 3;

        specification.ExposeWhere(criteria);

        Assert.Same(criteria, specification.Criteria);
    }

    [Fact]
    public void WhereCalledTwiceReplacesTheCriteria()
    {
        var specification = new WidgetSpecification();
        Expression<Func<Widget, bool>> first = w => w.Id > 3;
        Expression<Func<Widget, bool>> second = w => w.Id < 3;

        specification.ExposeWhere(first);
        specification.ExposeWhere(second);

        Assert.Same(second, specification.Criteria);
    }

    [Fact]
    public void AddIncludeAccumulates()
    {
        var specification = new WidgetSpecification();
        Expression<Func<Widget, object>> first = w => w.Name;
        Expression<Func<Widget, object>> second = w => w.Id;

        specification.ExposeAddInclude(first);
        specification.ExposeAddInclude(second);

        Assert.Equal([first, second], specification.Includes);
    }

    [Fact]
    public void ApplyOrderBySetsOrderBy()
    {
        var specification = new WidgetSpecification();
        Expression<Func<Widget, object>> key = w => w.Name;

        specification.ExposeOrderBy(key);

        Assert.Same(key, specification.OrderBy);
        Assert.Null(specification.OrderByDescending);
    }

    [Fact]
    public void ApplyOrderByDescendingSetsOrderByDescending()
    {
        var specification = new WidgetSpecification();
        Expression<Func<Widget, object>> key = w => w.Name;

        specification.ExposeOrderByDescending(key);

        Assert.Same(key, specification.OrderByDescending);
        Assert.Null(specification.OrderBy);
    }

    [Fact]
    public void ApplyThenBySetsTheSecondaryKey()
    {
        var specification = new WidgetSpecification();
        Expression<Func<Widget, object>> key = w => w.Id;

        specification.ExposeThenBy(key);

        Assert.Same(key, specification.ThenBy);
    }

    [Fact]
    public void ApplyPagingSetsSkipAndTake()
    {
        var specification = new WidgetSpecification();

        specification.ExposePaging(20, 10);

        Assert.Equal(20, specification.Skip);
        Assert.Equal(10, specification.Take);
    }

    [Fact]
    public void AsReadOnlySetsAsNoTracking()
    {
        var specification = new WidgetSpecification();

        specification.ExposeAsReadOnly();

        Assert.True(specification.AsNoTracking);
    }

    [Fact]
    public void IncludeSoftDeletedLiftsOnlyTheSoftDeleteFilter()
    {
        var specification = new WidgetSpecification();

        specification.ExposeIncludeSoftDeleted();

        Assert.True(specification.IgnoreSoftDeleteFilter);
        Assert.False(specification.IgnoreOrganisationScopeFilter);
    }

    [Fact]
    public void IgnoreOrganisationScopeLiftsOnlyTheScopeFilter()
    {
        var specification = new WidgetSpecification();

        specification.ExposeIgnoreOrganisationScope();

        Assert.True(specification.IgnoreOrganisationScopeFilter);
        Assert.False(specification.IgnoreSoftDeleteFilter);
    }

    public sealed record Widget(int Id, string Name);

    private sealed class WidgetSpecification : Specification<Widget>
    {
        public void ExposeWhere(Expression<Func<Widget, bool>> criteria) => Where(criteria);

        public void ExposeAddInclude(Expression<Func<Widget, object>> include) => AddInclude(include);

        public void ExposeOrderBy(Expression<Func<Widget, object>> key) => ApplyOrderBy(key);

        public void ExposeOrderByDescending(Expression<Func<Widget, object>> key) => ApplyOrderByDescending(key);

        public void ExposeThenBy(Expression<Func<Widget, object>> key) => ApplyThenBy(key);

        public void ExposePaging(int skip, int take) => ApplyPaging(skip, take);

        public void ExposeAsReadOnly() => AsReadOnly();

        public void ExposeIncludeSoftDeleted() => IncludeSoftDeleted();

        public void ExposeIgnoreOrganisationScope() => IgnoreOrganisationScope();
    }
}
