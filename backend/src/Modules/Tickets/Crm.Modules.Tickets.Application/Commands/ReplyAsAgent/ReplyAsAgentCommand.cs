using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.ReplyAsAgent;

/// <summary>An agent answers the customer. No RowVersion: a reply never conflicts with other edits.</summary>
[RequirePermission(CrmPermissions.TicketsWrite)]
public sealed record ReplyAsAgentCommand(Guid Id, string Body) : ICommand<TicketDto>;
