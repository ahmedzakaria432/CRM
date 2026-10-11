using System.Linq.Expressions;
using Crm.BuildingBlocks.Application.Specifications;

namespace Crm.BuildingBlocks.Tests.Support.Persistence;

/// <summary>A specification whose protected builders are public, so a test configures exactly what it needs.</summary>
public sealed class ConfigurableSpec<T> : Specification<T>
{
    public ConfigurableSpec<T> Filter(Expression<Func<T, bool>> criteria)
    {
        Where(criteria);
        return this;
    }

    public ConfigurableSpec<T> Order(Expression<Func<T, object>> key)
    {
        ApplyOrderBy(key);
        return this;
    }

    public ConfigurableSpec<T> OrderDescending(Expression<Func<T, object>> key)
    {
        ApplyOrderByDescending(key);
        return this;
    }

    public ConfigurableSpec<T> Then(Expression<Func<T, object>> key)
    {
        ApplyThenBy(key);
        return this;
    }

    public ConfigurableSpec<T> Page(int skip, int take)
    {
        ApplyPaging(skip, take);
        return this;
    }

    public ConfigurableSpec<T> ReadOnly()
    {
        AsReadOnly();
        return this;
    }

    public ConfigurableSpec<T> WithDeleted()
    {
        IncludeSoftDeleted();
        return this;
    }

    public ConfigurableSpec<T> AcrossScopes()
    {
        IgnoreOrganisationScope();
        return this;
    }
}
