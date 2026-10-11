using System.Reflection;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;
using FluentValidation;
using MediatR;

namespace Crm.Modules.Tickets.Tests.Application;

/// <summary>Reflection over the Application assembly: the module's shape is pinned, so a new use case without a
/// validator or an authorisation declaration fails here and not at start-up. The counts are hard-coded on purpose:
/// a later story that adds a use case updates this table, and the failure names the type that differs.</summary>
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

    private static Type[] Handlers() =>
        [.. Assembly.GetTypes().Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal) && t.IsClass)];

    private static Type RequestOf(Type handler) =>
        handler.GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)).GetGenericArguments()[0];

    private static string[] Named(IEnumerable<Type> types) => [.. types.Select(t => t.Name.Replace("Query", string.Empty, StringComparison.Ordinal).Replace("Command", string.Empty, StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

    [Fact]
    public void ThereAreNineteenCommandsAndThirteenQueries()
    {
        Type[] requests = Requests();

        Assert.Equal(19, requests.Count(t => typeof(ICommandMarker).IsAssignableFrom(t)));
        Assert.Equal(13, requests.Count(t => !typeof(ICommandMarker).IsAssignableFrom(t)));
    }

    [Fact]
    public void EveryCommandAndSevenQueriesHaveExactlyOneValidator()
    {
        Type[] requests = Requests();
        Type[] validated = [.. Validators().Select(v => ValidatedType(v) ?? throw new InvalidOperationException(v.Name))];

        Assert.Equal(26, validated.Length);
        Assert.Equal(validated.Length, validated.Distinct().Count());
        Assert.All(validated, v => Assert.Contains(v, requests));
        Assert.Equal(19, validated.Count(t => typeof(ICommandMarker).IsAssignableFrom(t)));
        Assert.Equal(
            ["GetCategory", "GetPortalTicket", "GetPortalTicketThread", "GetTicketById", "GetTicketThread", "ListPortalTickets", "SearchTickets"],
            Named(validated.Where(t => !typeof(ICommandMarker).IsAssignableFrom(t))));
    }

    [Fact]
    public void TheSixQueriesWithoutAValidatorAreTheKnownOnes()
    {
        Type[] validated = [.. Validators().Select(v => ValidatedType(v) ?? throw new InvalidOperationException(v.Name))];

        Assert.Equal(
            ["GetTicketQueueCounts", "ListCategories", "ListPortalTicketCategories", "ListTicketAgents", "ListTicketCategories", "ListTransferTargets"],
            Named(Requests().Where(t => !typeof(ICommandMarker).IsAssignableFrom(t) && !validated.Contains(t))));
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

    private static string[] WithPermission(string permission) =>
        Named(Requests().Where(t => t.GetCustomAttribute<RequirePermissionAttribute>()?.Permission == permission));

    [Fact]
    public void TheTicketsReadQueries() =>
        Assert.Equal(
            ["GetTicketById", "GetTicketQueueCounts", "GetTicketThread", "ListTicketAgents", "ListTicketCategories", "SearchTickets"],
            WithPermission(CrmPermissions.TicketsRead));

    [Fact]
    public void TheTicketsWriteCommands() =>
        Assert.Equal(
            ["AddInternalNote", "ChangeTicketCategory", "ChangeTicketPriority", "ClaimTicket", "CreateTicket", "ReceiveCustomerReply", "ReplyAsAgent", "TransitionTicket"],
            WithPermission(CrmPermissions.TicketsWrite));

    [Fact]
    public void TheTicketsAssignRequests() =>
        Assert.Equal(["AssignTicket", "ListTransferTargets", "TransferTicket", "UnassignTicket"], WithPermission(CrmPermissions.TicketsAssign));

    [Fact]
    public void TheAdminOrganisationRequests() =>
        Assert.Equal(
            ["CreateCategory", "DeactivateCategory", "GetCategory", "ListCategories", "ReactivateCategory", "UpdateCategory"],
            WithPermission(CrmPermissions.AdminOrganisation));

    [Fact]
    public void ThePortalTicketsRequests() =>
        Assert.Equal(
            ["GetPortalTicket", "GetPortalTicketThread", "ListPortalTicketCategories", "ListPortalTickets", "ReopenPortalTicket", "ReplyFromPortal", "RequestTicketClosure", "SubmitPortalTicket"],
            WithPermission(CrmPermissions.PortalTickets));

    [Fact]
    public void ThePermissionTableCoversEveryRequest() =>
        Assert.Equal(
            32,
            new[]
            {
                CrmPermissions.TicketsRead, CrmPermissions.TicketsWrite, CrmPermissions.TicketsAssign, CrmPermissions.AdminOrganisation,
                CrmPermissions.PortalTickets,
            }.Sum(p => WithPermission(p).Length));

    [Fact]
    public void NoIntegrationEventConsumerAndNoDomainEventHandlerExistsInTheModule()
    {
        Type[] all = Assembly.GetTypes();

        Assert.DoesNotContain(all, t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventConsumer<>)));
        Assert.DoesNotContain(all, t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>)));
        Assert.DoesNotContain(
            typeof(Crm.Modules.Tickets.Infrastructure.TicketsModule).Assembly.GetTypes(),
            t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>)));
    }

    [Fact]
    public void NothingIsCacheableAndNoCommandInvalidatesACache()
    {
        Type[] all = Assembly.GetTypes();

        Assert.DoesNotContain(all, t => typeof(ICacheableQuery).IsAssignableFrom(t));
        Assert.DoesNotContain(all, t => typeof(ICacheInvalidatingCommand).IsAssignableFrom(t));
    }

    [Fact]
    public void EveryHandlerInjectsTheModuleUnitOfWorkForCommandsAndNeverTheBareOne()
    {
        Type[] handlers = Handlers();

        Assert.Equal(32, handlers.Length);
        foreach (Type handler in handlers)
        {
            Type[] parameters = [.. handler.GetConstructors().Single().GetParameters().Select(p => p.ParameterType)];

            Assert.DoesNotContain(typeof(Crm.BuildingBlocks.Application.Abstractions.IUnitOfWork), parameters);
            if (typeof(ICommandMarker).IsAssignableFrom(RequestOf(handler)))
            {
                Assert.Contains(typeof(ITicketsUnitOfWork), parameters);
            }
        }
    }

    [Fact]
    public void ExactlyFourteenHandlersTakeAnEventBus()
    {
        Type[] withBus = [.. Handlers().Where(t => t.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IEventBus)))];

        Assert.Equal(14, withBus.Length);
        Assert.Equal(
            [
                "AssignTicket", "ChangeTicketCategory", "ChangeTicketPriority", "ClaimTicket", "CreateTicket", "ReceiveCustomerReply", "ReopenPortalTicket",
                "ReplyAsAgent", "ReplyFromPortal", "RequestTicketClosure", "SubmitPortalTicket", "TransferTicket", "TransitionTicket", "UnassignTicket",
            ],
            Named(withBus.Select(RequestOf)));
    }

    [Fact]
    public void AddInternalNoteAndTheCategoryCommandsPublishNothing()
    {
        string[] silent = ["AddInternalNoteCommandHandler", "CreateCategoryCommandHandler", "UpdateCategoryCommandHandler", "DeactivateCategoryCommandHandler", "ReactivateCategoryCommandHandler"];

        foreach (Type handler in Handlers().Where(h => silent.Contains(h.Name)))
        {
            Assert.DoesNotContain(handler.GetConstructors().Single().GetParameters(), p => p.ParameterType == typeof(IEventBus));
        }

        Assert.Equal(5, Handlers().Count(h => silent.Contains(h.Name)));
    }

    [Fact]
    public void HandlersThatPublishTakeTheAssemblerOrTheMapperSoTheyReadBeforePublish()
    {
        foreach (Type handler in Handlers().Where(t => t.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IEventBus))))
        {
            Type[] parameters = [.. handler.GetConstructors().Single().GetParameters().Select(p => p.ParameterType)];

            Assert.True(parameters.Contains(typeof(TicketDtoAssembler)) || parameters.Contains(typeof(PortalTicketMapper)), handler.Name);
        }
    }

    [Fact]
    public void TheContractsExposeNoTicketsModuleApi()
    {
        Type[] contracts = [.. typeof(Crm.Modules.Tickets.Contracts.IntegrationEvents.TicketCreatedIntegrationEvent).Assembly.GetExportedTypes()];

        Assert.DoesNotContain(contracts, t => t.IsInterface);
    }

    [Fact]
    public void TheModuleHasNoControllersInItsApplicationAssembly()
    {
        Assert.DoesNotContain(Assembly.GetTypes(), t => t.Name.EndsWith("Controller", StringComparison.Ordinal));
    }
}
