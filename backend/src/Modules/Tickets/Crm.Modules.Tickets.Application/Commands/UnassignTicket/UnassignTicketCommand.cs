using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.UnassignTicket;

[RequirePermission(CrmPermissions.TicketsAssign)]
public sealed record UnassignTicketCommand(Guid Id, byte[] RowVersion) : ICommand<TicketDto>;
