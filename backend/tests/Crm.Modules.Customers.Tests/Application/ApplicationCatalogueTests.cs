using System.Reflection;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Customers.Application;
using FluentValidation;
using MediatR;

namespace Crm.Modules.Customers.Tests.Application;

/// <summary>Reflection over the Application assembly: the module's shape is pinned, so a new use case without a
/// validator or an authorisation declaration fails here and not at start-up.</summary>
public sealed class ApplicationCatalogueTests
{
    private static readonly Assembly Assembly = AssemblyReference.Assembly;

    private static Type[] Requests() =>
        [.. Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBaseRequest).IsAssignableFrom(t))];

    private static Type[] Validators() =>
        [.. Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && ValidatedType(t) is not null)];

    private static Type? ValidatedType(Type validator)
    {
        for (Type? type = validator; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        return null;
    }

    [Fact]
    public void ThereAreElevenCommandsAndSevenQueries()
    {
        Type[] requests = Requests();

        Assert.Equal(11, requests.Count(t => typeof(ICommandMarker).IsAssignableFrom(t)));
        Assert.Equal(7, requests.Count(t => !typeof(ICommandMarker).IsAssignableFrom(t)));
    }

    [Fact]
    public void EveryRequestHasExactlyOneValidatorAndNoValidatorIsWithoutARequest()
    {
        Type[] requests = Requests();
        Type[] validated = [.. Validators().Select(v => ValidatedType(v) ?? throw new InvalidOperationException(v.Name))];

        Assert.Equal(18, validated.Length);
        Assert.Equal(requests.OrderBy(t => t.Name, StringComparer.Ordinal), validated.OrderBy(t => t.Name, StringComparer.Ordinal));
        Assert.Equal(validated.Length, validated.Distinct().Count());
    }

    [Fact]
    public void EveryRequestDeclaresExactlyOneAuthorisationAttribute()
    {
        foreach (Type request in Requests())
        {
            int declared =
                (request.GetCustomAttribute<RequirePermissionAttribute>(inherit: false) is null ? 0 : 1)
                + (request.GetCustomAttribute<RequireAuthenticatedUserAttribute>(inherit: false) is null ? 0 : 1)
                + (request.GetCustomAttribute<AllowAnonymousRequestAttribute>(inherit: false) is null ? 0 : 1);

            Assert.True(declared == 1, request.Name);
        }
    }

    [Fact]
    public void TheHostsStartUpGuardAcceptsTheAssembly() =>
        RequestAuthorizationGuard.EnsureEveryRequestDeclares([Assembly]);

    [Fact]
    public void ThePermissionTableIsWriteForCommandsReadForStaffQueriesAndAnonymousForThePortalLookups()
    {
        foreach (Type request in Requests())
        {
            string? permission = request.GetCustomAttribute<RequirePermissionAttribute>()?.Permission;
            bool anonymous = request.GetCustomAttribute<AllowAnonymousRequestAttribute>() is not null;

            if (typeof(ICommandMarker).IsAssignableFrom(request))
            {
                Assert.Equal(CrmPermissions.CustomersWrite, permission);
            }
            else if (request.Name is "FindPortalContactsByEmailQuery" or "GetPortalContactQuery")
            {
                Assert.True(anonymous, request.Name);
                Assert.Null(permission);
            }
            else
            {
                Assert.Equal(CrmPermissions.CustomersRead, permission);
            }
        }
    }

    [Fact]
    public void NoIntegrationEventConsumerAndNoDomainEventHandlerExistsInTheModule()
    {
        Type[] all = Assembly.GetTypes();

        Assert.DoesNotContain(all, t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventConsumer<>)));
        Assert.DoesNotContain(all, t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>)));
    }

    [Fact]
    public void EveryHandlerInjectsTheModuleUnitOfWorkAndNeverTheBareOne()
    {
        Type[] handlers = [.. Assembly.GetTypes().Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal) && t.IsClass)];

        Assert.Equal(18, handlers.Length);
        foreach (Type handler in handlers)
        {
            Type[] parameters = [.. handler.GetConstructors().Single().GetParameters().Select(p => p.ParameterType)];

            Assert.DoesNotContain(typeof(Crm.BuildingBlocks.Application.Abstractions.IUnitOfWork), parameters);
            if (typeof(ICommandMarker).IsAssignableFrom(RequestOf(handler)))
            {
                Assert.Contains(typeof(ICustomersUnitOfWork), parameters);
            }
        }
    }

    private static Type RequestOf(Type handler) =>
        handler.GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)).GetGenericArguments()[0];

    [Fact]
    public void OnlyTheFourCustomerCommandHandlersTakeAnEventBus()
    {
        Type[] withBus =
        [
            .. Assembly.GetTypes()
                .Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal) && t.IsClass)
                .Where(t => t.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IEventBus)))
        ];

        Assert.Equal(
            ["CreateCustomerCommandHandler", "DeactivateCustomerCommandHandler", "ReactivateCustomerCommandHandler", "UpdateCustomerCommandHandler"],
            withBus.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal));
    }
}
