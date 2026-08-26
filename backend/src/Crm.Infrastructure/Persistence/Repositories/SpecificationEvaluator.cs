using Crm.Application.Common.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

public static class SpecificationEvaluator
{
    public static IQueryable<T> GetQuery<T>(IQueryable<T> inputQuery, Specification<T> specification)
        where T : class
    {
        IQueryable<T> query = inputQuery;

        if (specification.IgnoreSoftDeleteFilter)
        {
            query = query.IgnoreQueryFilters([CrmDbContext.SoftDeleteFilterName]);
        }

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));

        if (specification.OrderBy is not null)
        {
            query = query.OrderBy(specification.OrderBy);
        }
        else if (specification.OrderByDescending is not null)
        {
            query = query.OrderByDescending(specification.OrderByDescending);
        }

        // Paging goes after ordering. Skip/Take on an unordered query returns non-deterministic
        // rows on SQL Server, and with no test suite that surfaces as an intermittent,
        // unreproducible bug report rather than a build failure.
        if (specification.Skip.HasValue)
        {
            query = query.Skip(specification.Skip.Value);
        }

        if (specification.Take.HasValue)
        {
            query = query.Take(specification.Take.Value);
        }

        if (specification.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query;
    }
}
