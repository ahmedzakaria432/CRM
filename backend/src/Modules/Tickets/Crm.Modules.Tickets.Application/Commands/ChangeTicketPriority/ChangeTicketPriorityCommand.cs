using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.ChangeTicketPriority;

[RequirePermission(CrmPermissions.TicketsWrite)]
public sealed record ChangeTicketPriorityCommand(Guid Id, string Priority, byte[] RowVersion) : ICommand<TicketDto>;
