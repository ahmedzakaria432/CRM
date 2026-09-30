using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.CreateTicket;

[RequirePermission(CrmPermissions.TicketsWrite)]
public sealed record CreateTicketCommand(
    Guid CustomerId, Guid? ContactId, string Subject, string Description, string? Priority, Guid? CategoryId, Guid? DepartmentId)
    : ICommand<TicketDto>;
