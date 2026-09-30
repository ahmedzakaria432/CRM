using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListTransferTargets;

/// <summary>Every active department of the tenant - where a ticket can be transferred (tickets.assign).</summary>
[RequirePermission(CrmPermissions.TicketsAssign)]
public sealed record ListTransferTargetsQuery : IRequest<Result<IReadOnlyList<TicketDepartmentRefDto>>>;
