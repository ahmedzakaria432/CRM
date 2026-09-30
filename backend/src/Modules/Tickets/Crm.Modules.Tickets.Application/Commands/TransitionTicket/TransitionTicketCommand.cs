using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.TransitionTicket;

/// <summary>Action is one of Open, AwaitCustomer, PutOnHold, Resolve, Close, Reopen, Cancel.</summary>
[RequirePermission(CrmPermissions.TicketsWrite)]
public sealed record TransitionTicketCommand(Guid Id, string Action, byte[] RowVersion) : ICommand<TicketDto>;
