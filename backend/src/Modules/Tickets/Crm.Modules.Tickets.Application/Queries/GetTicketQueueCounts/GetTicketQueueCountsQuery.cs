using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetTicketQueueCounts;

[RequirePermission(CrmPermissions.TicketsRead)]
public sealed record GetTicketQueueCountsQuery : IRequest<Result<TicketQueueCountsDto>>;
