using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.AssignTicket;

[RequirePermission(CrmPermissions.TicketsAssign)]
public sealed record AssignTicketCommand(Guid Id, Guid AgentId, byte[] RowVersion) : ICommand<TicketDto>;
