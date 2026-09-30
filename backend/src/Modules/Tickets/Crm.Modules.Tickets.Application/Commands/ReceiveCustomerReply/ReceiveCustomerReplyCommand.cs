using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.ReceiveCustomerReply;

/// <summary>An agent records a reply the customer sent outside the CRM (phone, email) until channels exist (22-27).
/// Author = the ticket contact.</summary>
[RequirePermission(CrmPermissions.TicketsWrite)]
public sealed record ReceiveCustomerReplyCommand(Guid Id, string Body) : ICommand<TicketDto>;
