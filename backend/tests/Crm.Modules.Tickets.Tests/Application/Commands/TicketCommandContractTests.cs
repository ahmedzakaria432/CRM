using System.Reflection;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Tickets.Application.Commands.AddInternalNote;
using Crm.Modules.Tickets.Application.Commands.AssignTicket;
using Crm.Modules.Tickets.Application.Commands.ChangeTicketCategory;
using Crm.Modules.Tickets.Application.Commands.ChangeTicketPriority;
using Crm.Modules.Tickets.Application.Commands.ClaimTicket;
using Crm.Modules.Tickets.Application.Commands.CreateTicket;
using Crm.Modules.Tickets.Application.Commands.ReceiveCustomerReply;
using Crm.Modules.Tickets.Application.Commands.ReplyAsAgent;
using Crm.Modules.Tickets.Application.Commands.TransferTicket;
using Crm.Modules.Tickets.Application.Commands.TransitionTicket;
using Crm.Modules.Tickets.Application.Commands.UnassignTicket;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Commands;

/// <summary>What the pipeline reads from the agent ticket commands: the permission, the response type and the row version.</summary>
public sealed class TicketCommandContractTests
{
    private static readonly (Type Command, string Permission, bool HasRowVersion)[] Commands =
    [
        (typeof(CreateTicketCommand), CrmPermissions.TicketsWrite, false),
        (typeof(TransitionTicketCommand), CrmPermissions.TicketsWrite, true),
        (typeof(ClaimTicketCommand), CrmPermissions.TicketsWrite, false),
        (typeof(ChangeTicketPriorityCommand), CrmPermissions.TicketsWrite, true),
        (typeof(ChangeTicketCategoryCommand), CrmPermissions.TicketsWrite, true),
        (typeof(ReplyAsAgentCommand), CrmPermissions.TicketsWrite, false),
        (typeof(ReceiveCustomerReplyCommand), CrmPermissions.TicketsWrite, false),
        (typeof(AddInternalNoteCommand), CrmPermissions.TicketsWrite, false),
        (typeof(AssignTicketCommand), CrmPermissions.TicketsAssign, true),
        (typeof(UnassignTicketCommand), CrmPermissions.TicketsAssign, true),
        (typeof(TransferTicketCommand), CrmPermissions.TicketsAssign, true),
    ];

    [Fact]
    public void ThereAreElevenAgentTicketCommands() => Assert.Equal(11, Commands.Length);

    [Fact]
    public void EachCommandDeclaresItsPermission()
    {
        foreach ((Type command, string permission, _) in Commands)
        {
            Assert.True(permission == Stubs.PermissionOf(command), command.Name);
        }
    }

    [Fact]
    public void TicketsWriteCoversEightCommandsAndTicketsAssignThree()
    {
        Assert.Equal(8, Commands.Count(c => c.Permission == CrmPermissions.TicketsWrite));
        Assert.Equal(3, Commands.Count(c => c.Permission == CrmPermissions.TicketsAssign));
    }

    [Fact]
    public void EachCommandIsACommandOfTicketDto()
    {
        foreach ((Type command, _, _) in Commands)
        {
            Assert.True(typeof(ICommand<TicketDto>).IsAssignableFrom(command), command.Name);
        }
    }

    [Fact]
    public void TheRowVersionParameterIsPresentExactlyWhereTheContractSays()
    {
        foreach ((Type command, _, bool hasRowVersion) in Commands)
        {
            ParameterInfo[] parameters = command.GetConstructors().Single().GetParameters();

            Assert.True(
                hasRowVersion == parameters.Any(p => p.Name == "RowVersion" && p.ParameterType == typeof(byte[])), command.Name);
        }
    }

    [Fact]
    public void NoCommandInvalidatesACache()
    {
        foreach ((Type command, _, _) in Commands)
        {
            Assert.False(typeof(ICacheInvalidatingCommand).IsAssignableFrom(command), command.Name);
        }
    }
}
