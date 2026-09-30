using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.SubmitPortalTicket;

/// <summary>The portal customer raises a ticket: channel Portal, the owning department of the customer, priority
/// Normal. The category is optional and must be active and customer-visible in that department (Story 47).</summary>
[RequirePermission(CrmPermissions.PortalTickets)]
public sealed record SubmitPortalTicketCommand(string Subject, string Description, Guid? CategoryId) : ICommand<PortalTicketDto>;
