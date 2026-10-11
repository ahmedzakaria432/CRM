using System.Reflection;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Tickets.Application.Commands.CreateCategory;
using Crm.Modules.Tickets.Application.Commands.DeactivateCategory;
using Crm.Modules.Tickets.Application.Commands.ReactivateCategory;
using Crm.Modules.Tickets.Application.Commands.ReopenPortalTicket;
using Crm.Modules.Tickets.Application.Commands.ReplyFromPortal;
using Crm.Modules.Tickets.Application.Commands.RequestTicketClosure;
using Crm.Modules.Tickets.Application.Commands.SubmitPortalTicket;
using Crm.Modules.Tickets.Application.Commands.UpdateCategory;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Commands;

public sealed class PortalCommandContractTests
{
    private static readonly Type[] PortalCommands =
    [
        typeof(SubmitPortalTicketCommand), typeof(ReplyFromPortalCommand), typeof(RequestTicketClosureCommand), typeof(ReopenPortalTicketCommand),
    ];

    private static readonly Type[] CategoryCommands =
    [
        typeof(CreateCategoryCommand), typeof(UpdateCategoryCommand), typeof(DeactivateCategoryCommand), typeof(ReactivateCategoryCommand),
    ];

    [Fact]
    public void ThePortalCommandsRequirePortalTicketsAndReturnAPortalDto()
    {
        foreach (Type command in PortalCommands)
        {
            Assert.True(CrmPermissions.PortalTickets == Stubs.PermissionOf(command), command.Name);
            Assert.True(typeof(ICommand<PortalTicketDto>).IsAssignableFrom(command), command.Name);
        }
    }

    [Fact]
    public void NoPortalCommandCarriesARowVersion()
    {
        foreach (Type command in PortalCommands)
        {
            Assert.DoesNotContain(command.GetConstructors().Single().GetParameters(), p => p.Name == "RowVersion");
        }
    }

    [Fact]
    public void ThePortalTicketHandlersTakeThePortalMapperAndNotTheAgentAssembler()
    {
        Type[] handlers =
        [
            typeof(SubmitPortalTicketCommandHandler), typeof(ReplyFromPortalCommandHandler), typeof(RequestTicketClosureCommandHandler),
            typeof(ReopenPortalTicketCommandHandler),
        ];

        foreach (Type handler in handlers)
        {
            Assert.Contains(handler.GetConstructors().Single().GetParameters(), p => p.ParameterType == typeof(PortalTicketMapper));
            Assert.DoesNotContain(handler.GetConstructors().Single().GetParameters(), p => p.ParameterType == typeof(TicketDtoAssembler));
        }
    }

    [Fact]
    public void TheCategoryCommandsRequireAdminOrganisationAndReturnACategoryDto()
    {
        foreach (Type command in CategoryCommands)
        {
            Assert.True(CrmPermissions.AdminOrganisation == Stubs.PermissionOf(command), command.Name);
            Assert.True(typeof(ICommand<TicketCategoryDto>).IsAssignableFrom(command), command.Name);
        }
    }

    [Fact]
    public void TheCategoryCommandsCarryARowVersionExceptCreate()
    {
        foreach (Type command in CategoryCommands)
        {
            bool has = command.GetConstructors().Single().GetParameters().Any(p => p.Name == "RowVersion");

            Assert.True(has == (command != typeof(CreateCategoryCommand)), command.Name);
        }
    }

    [Fact]
    public void NoCategoryHandlerPublishesAnEvent()
    {
        Type[] handlers =
        [
            typeof(CreateCategoryCommandHandler), typeof(UpdateCategoryCommandHandler), typeof(DeactivateCategoryCommandHandler),
            typeof(ReactivateCategoryCommandHandler),
        ];

        foreach (Type handler in handlers)
        {
            ParameterInfo[] parameters = handler.GetConstructors().Single().GetParameters();

            Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(IEventBus));
        }
    }
}
