using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Paging;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.SearchTickets;

public sealed class SearchTicketsQueryHandler(
    IRepository<Ticket, TicketId> tickets,
    ITicketReadModel readModel,
    ICurrentUserService currentUser,
    TicketDtoAssembler assembler)
    : IRequestHandler<SearchTicketsQuery, Result<PagedResult<TicketListItemDto>>>
{
    public async Task<Result<PagedResult<TicketListItemDto>>> Handle(SearchTicketsQuery request, CancellationToken cancellationToken)
    {
        TicketQueueKind queue = TicketQueueKind.All;
        if (request.Queue is not null && !TicketQueueParser.TryParse(request.Queue, out queue))
        {
            queue = TicketQueueKind.All;
        }

        TicketSort sort = TicketSort.CreatedAt;
        if (request.Sort is not null && !TicketSortParser.TryParse(request.Sort, out sort))
        {
            sort = TicketSort.CreatedAt;
        }

        IReadOnlyList<TicketStatus> statuses = [.. (request.Statuses ?? []).Select(s => Enum.Parse<TicketStatus>(s, ignoreCase: true)).Distinct()];
        IReadOnlyList<TicketPriority> priorities = [.. (request.Priorities ?? []).Select(p => Enum.Parse<TicketPriority>(p, ignoreCase: true)).Distinct()];
        Guid me = CurrentActor.Id(currentUser);

        IReadOnlyList<TicketListRow> page = await readModel.ListAsync(
            new TicketSearchSpecification(
                queue, me, request.Search, statuses, priorities, request.CategoryId, request.AssigneeId, request.DepartmentId,
                request.BranchId, request.CustomerId, sort, request.Descending, request.Page, request.PageSize),
            cancellationToken);

        int total = await tickets.CountAsync(
            new TicketSearchSpecification(
                queue, me, request.Search, statuses, priorities, request.CategoryId, request.AssigneeId, request.DepartmentId,
                request.BranchId, request.CustomerId, sort, request.Descending, null, null),
            cancellationToken);

        return Result.Success(new PagedResult<TicketListItemDto>(
            await assembler.ToListItemsAsync(page, cancellationToken), total, request.Page, request.PageSize));
    }
}
