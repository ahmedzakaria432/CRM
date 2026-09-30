using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.ClaimTicket;

/// <summary>Takes no client rowversion: the loaded rowversion is the concurrency token (two simultaneous claims have one winner).</summary>
[RequirePermission(CrmPermissions.TicketsWrite)]
public sealed record ClaimTicketCommand(Guid Id) : ICommand<TicketDto>;
