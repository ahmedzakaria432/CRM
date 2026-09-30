using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.ReplyFromPortal;

/// <summary>The portal customer writes on one of their own tickets (Story 47). A reply to a Resolved or PendingCustomer
/// ticket reopens it (the Story 18 rule). No rowVersion: a clash with an agent is a 409 the portal shows as try again.</summary>
[RequirePermission(CrmPermissions.PortalTickets)]
public sealed record ReplyFromPortalCommand(Guid Id, string Body) : ICommand<PortalTicketDto>;
