using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Application.Abstractions;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel;

namespace Crm.Modules.Customers.Tests.Support;

/// <summary>Small helpers that keep handler tests about behaviour rather than NSubstitute plumbing.</summary>
public static class Stubs
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static void Holds(this IRepository<Customer, CustomerId> repository, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(customer);

        repository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Customer?>(customer));
    }

    public static void CountsAs(this IRepository<Customer, CustomerId> repository, int count)
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.CountAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(count));
    }

    /// <summary>ListAsync(specification) evaluates criteria, order and paging of the specification in memory.</summary>
    public static void Lists(this IRepository<Customer, CustomerId> repository, params Customer[] items)
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.ListAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<Customer>>(Matching(call.Arg<Specification<Customer>>(), items)));
    }

    /// <summary>CountAsync(specification) counts the items the criteria keep, ignoring order and paging.</summary>
    public static void Counts(this IRepository<Customer, CustomerId> repository, params Customer[] items)
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.CountAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<Specification<Customer>>().Criteria is { } criteria ? items.Count(criteria.Compile()) : items.Length));
    }

    /// <summary>FirstOrDefaultAsync(specification) returns the first of the items the specification keeps.</summary>
    public static void FindsFirstOf(this IRepository<Customer, CustomerId> repository, params Customer[] items)
    {
        ArgumentNullException.ThrowIfNull(repository);

        repository.FirstOrDefaultAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Matching(call.Arg<Specification<Customer>>(), items).FirstOrDefault()));
    }

    /// <summary>The criteria, the order (with the ThenBy tie-break) and the paging of the specification, in memory.</summary>
    public static List<T> Matching<T>(Specification<T> specification, params T[] items)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(items);

        IEnumerable<T> query = items;
        if (specification.Criteria is { } criteria)
        {
            query = query.Where(criteria.Compile());
        }

        IOrderedEnumerable<T>? ordered = null;
        if (specification.OrderBy is { } orderBy)
        {
            ordered = query.OrderBy(orderBy.Compile());
        }
        else if (specification.OrderByDescending is { } orderByDescending)
        {
            ordered = query.OrderByDescending(orderByDescending.Compile());
        }

        if (ordered is not null)
        {
            query = specification.ThenBy is { } thenBy ? ordered.ThenBy(thenBy.Compile()) : ordered;
        }

        if (specification.Skip is { } skip)
        {
            query = query.Skip(skip);
        }

        if (specification.Take is { } take)
        {
            query = query.Take(take);
        }

        return [.. query];
    }

    /// <summary>ICustomerNumberGenerator that returns C-000001, C-000002 ... starting at <paramref name="first"/>.</summary>
    public static ICustomerNumberGenerator Numbers(long first = 1)
    {
        var generator = Substitute.For<ICustomerNumberGenerator>();
        long next = first;
        generator.NextAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(CustomerNumber.FromSequence(next++)));
        return generator;
    }

    /// <summary>An IEventBus substitute whose published events are collected in order.</summary>
    public static IEventBus RecordingBus(List<IIntegrationEvent> published)
    {
        ArgumentNullException.ThrowIfNull(published);

        var bus = Substitute.For<IEventBus>();
        bus.When(b => b.Publish(Arg.Any<IIntegrationEvent>())).Do(call => published.Add(call.Arg<IIntegrationEvent>()));
        return bus;
    }

    public static string? PermissionOf(Type request) => request.GetCustomAttribute<RequirePermissionAttribute>()?.Permission;

    public static bool HasAttribute<TAttribute>(Type request)
        where TAttribute : Attribute => request.GetCustomAttribute<TAttribute>() is not null;
}
