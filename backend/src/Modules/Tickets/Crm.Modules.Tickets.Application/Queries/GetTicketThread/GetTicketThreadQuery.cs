using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetTicketThread;

/// <summary>The ticket thread, oldest first, cursor-paged by Sequence (Story 18). Visibility: public, internal,
/// system (repeatable; empty = all three).</summary>
[RequirePermission(CrmPermissions.TicketsRead)]
public sealed record GetTicketThreadQuery(Guid TicketId, int? After, int Limit, IReadOnlyList<string> Visibility)
    : IRequest<Result<TicketThreadPageDto>>;
