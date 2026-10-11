using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.SharedKernel;

namespace Crm.Modules.Identity.Tests.Support;

/// <summary>Small helpers that keep handler tests about behaviour rather than NSubstitute plumbing.</summary>
public static class Stubs
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static void Holds<TAggregate, TId>(this IRepository<TAggregate, TId> repository, TAggregate aggregate)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(aggregate);

        repository.GetByIdAsync(aggregate.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TAggregate?>(aggregate));
    }

    public static void CountsAs<TAggregate, TId>(this IRepository<TAggregate, TId> repository, int count)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.CountAsync(Arg.Any<Specification<TAggregate>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(count));
    }

    /// <summary>ListAsync(specification) evaluates the criteria of the specification in memory over the items.</summary>
    public static void Lists<TAggregate, TId>(this IRepository<TAggregate, TId> repository, params TAggregate[] items)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.ListAsync(Arg.Any<Specification<TAggregate>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<TAggregate>>(Matching(call.Arg<Specification<TAggregate>>(), items)));
    }

    /// <summary>FirstOrDefaultAsync(specification) returns the first of the items the specification keeps.</summary>
    public static void FindsFirstOf<TAggregate, TId>(this IRepository<TAggregate, TId> repository, params TAggregate[] items)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.FirstOrDefaultAsync(Arg.Any<Specification<TAggregate>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Matching(call.Arg<Specification<TAggregate>>(), items).FirstOrDefault()));
    }

    /// <summary>FirstOrDefaultAsync(any specification) returns the item (null means nothing found).</summary>
    public static void Finds<TAggregate, TId>(this IRepository<TAggregate, TId> repository, TAggregate? item)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.FirstOrDefaultAsync(Arg.Any<Specification<TAggregate>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(item));
    }

    public static List<T> Matching<T>(Specification<T> specification, params T[] items)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(items);

        IEnumerable<T> query = items;
        if (specification.Criteria is { } criteria)
        {
            query = query.Where(criteria.Compile());
        }

        return [.. query];
    }

    public static void Generates(this IRefreshTokenGenerator generator, string token, string hash)
    {
        ArgumentNullException.ThrowIfNull(generator);

        generator.Create().Returns(new NewRefreshToken(token, hash));
    }

    public static string? PermissionOf(Type request) => request.GetCustomAttribute<RequirePermissionAttribute>()?.Permission;

    public static bool HasAttribute<TAttribute>(Type request)
        where TAttribute : Attribute => request.GetCustomAttribute<TAttribute>() is not null;
}
