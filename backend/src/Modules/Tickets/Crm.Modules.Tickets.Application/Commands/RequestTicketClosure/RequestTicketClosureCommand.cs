using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.RequestTicketClosure;

/// <summary>The customer says the problem is solved: the ticket is Resolved (Story 47, D10).</summary>
[RequirePermission(CrmPermissions.PortalTickets)]
public sealed record RequestTicketClosureCommand(Guid Id) : ICommand<PortalTicketDto>;
