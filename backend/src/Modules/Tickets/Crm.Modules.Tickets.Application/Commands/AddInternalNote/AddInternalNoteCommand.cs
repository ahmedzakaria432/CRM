using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.AddInternalNote;

/// <summary>A staff-only note. No RowVersion; allowed in every status.</summary>
[RequirePermission(CrmPermissions.TicketsWrite)]
public sealed record AddInternalNoteCommand(Guid Id, string Body) : ICommand<TicketDto>;
