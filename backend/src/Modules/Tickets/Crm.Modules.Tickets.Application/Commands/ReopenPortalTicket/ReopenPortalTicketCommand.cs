using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.ReopenPortalTicket;

/// <summary>The customer reopens a Resolved ticket; a Closed one is 422 (Story 47, D10).</summary>
[RequirePermission(CrmPermissions.PortalTickets)]
public sealed record ReopenPortalTicketCommand(Guid Id) : ICommand<PortalTicketDto>;
