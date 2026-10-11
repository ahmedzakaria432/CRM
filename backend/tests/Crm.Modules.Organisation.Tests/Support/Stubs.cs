using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Tests.Support;

/// <summary>Small helpers that keep handler tests about behaviour rather than NSubstitute plumbing.</summary>
public static class Stubs
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>GetByIdAsync(aggregate.Id) returns the aggregate. An id nobody stubbed answers null (not found).</summary>
    public static void Holds<TAggregate, TId>(this IRepository<TAggregate, TId> repository, TAggregate aggregate)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(aggregate);

        repository.GetByIdAsync(aggregate.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TAggregate?>(aggregate));
    }

    /// <summary>CountAsync(any specification) returns <paramref name="count"/>.</summary>
    public static void CountsAs<TAggregate, TId>(this IRepository<TAggregate, TId> repository, int count)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.CountAsync(Arg.Any<Specification<TAggregate>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(count));
    }

    /// <summary>ListAsync(specification) evaluates the specification's criteria and ordering in memory over
    /// <paramref name="items"/>. (The real ordering by a value-object key cannot run on EF InMemory, and a substituted
    /// repository must not ignore the specification the handler built.)</summary>
    public static void Lists<TAggregate, TId>(this IRepository<TAggregate, TId> repository, params TAggregate[] items)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.ListAsync(Arg.Any<Specification<TAggregate>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<TAggregate>>(Matching(call.Arg<Specification<TAggregate>>(), items)));
    }

    /// <summary>FirstOrDefaultAsync(specification) returns the first of <paramref name="items"/> the specification keeps.</summary>
    public static void FindsFirstOf<TAggregate, TId>(this IRepository<TAggregate, TId> repository, params TAggregate[] items)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.FirstOrDefaultAsync(Arg.Any<Specification<TAggregate>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Matching(call.Arg<Specification<TAggregate>>(), items).FirstOrDefault()));
    }

    /// <summary>The items a specification keeps, in the order it asks for, evaluated in memory (no EF filters).</summary>
    public static List<T> Matching<T>(Specification<T> specification, params T[] items)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(items);

        IEnumerable<T> query = items;
        if (specification.Criteria is { } criteria)
        {
            query = query.Where(criteria.Compile());
        }

        if (specification.OrderBy is { } orderBy)
        {
            Func<T, object> key = orderBy.Compile();
            query = query.OrderBy(item => SortKey(key(item)), StringComparer.Ordinal);
        }

        return [.. query];
    }

    private static string SortKey(object key) => key switch
    {
        OrganisationCode code => code.Value,
        _ => key.ToString() ?? string.Empty,
    };

    public static string? PermissionOf(Type request) => request.GetCustomAttribute<RequirePermissionAttribute>()?.Permission;

    public static bool HasAttribute<TAttribute>(Type request)
        where TAttribute : Attribute => request.GetCustomAttribute<TAttribute>() is not null;
}
