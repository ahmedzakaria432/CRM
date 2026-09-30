using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Paging;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.SearchTickets;

/// <summary>Queue is mine, unassigned or all (default). Statuses and Priorities are repeatable filters.</summary>
[RequirePermission(CrmPermissions.TicketsRead)]
public sealed record SearchTicketsQuery(
    string? Queue, string? Search, IReadOnlyList<string>? Statuses, IReadOnlyList<string>? Priorities, Guid? CategoryId,
    Guid? AssigneeId, Guid? DepartmentId, Guid? BranchId, Guid? CustomerId, string? Sort, bool Descending = true, int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<TicketListItemDto>>>;
