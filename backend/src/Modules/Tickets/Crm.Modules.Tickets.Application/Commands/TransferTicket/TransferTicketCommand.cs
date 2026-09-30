using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.TransferTicket;

[RequirePermission(CrmPermissions.TicketsAssign)]
public sealed record TransferTicketCommand(Guid Id, Guid DepartmentId, byte[] RowVersion) : ICommand<TicketDto>;
