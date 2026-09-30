using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Paging;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListPortalTickets;

public sealed class ListPortalTicketsQueryHandler(
    IRepository<Ticket, TicketId> tickets,
    IPortalTicketReadModel readModel,
    ICurrentUserService currentUser,
    PortalTicketMapper mapper)
    : IRequestHandler<ListPortalTicketsQuery, Result<PagedResult<PortalTicketSummaryDto>>>
{
    public async Task<Result<PagedResult<PortalTicketSummaryDto>>> Handle(ListPortalTicketsQuery request, CancellationToken cancellationToken)
    {
        if (PortalCaller.Grant(currentUser) is not { } grant)
        {
            return Result.Failure<PagedResult<PortalTicketSummaryDto>>(TicketsErrors.PortalOnly());
        }

        // A status names one portal status inside the tab; without one the whole tab is listed.
        IReadOnlyList<TicketStatus> tab = PortalTicketStatuses.TabStatuses(request.Tab);
        IReadOnlyList<TicketStatus> statuses = string.IsNullOrWhiteSpace(request.Status)
            ? tab
            : [.. PortalTicketStatuses.Statuses(request.Status).Where(tab.Contains)];

        if (statuses.Count == 0)
        {
            // A status that does not belong to the tab: nothing matches (an empty list would mean "any status").
            return Result.Success(new PagedResult<PortalTicketSummaryDto>([], 0, request.Page, request.PageSize));
        }

        IReadOnlyList<PortalTicketRow> page = await readModel.ListAsync(
            new PortalTicketsSpecification(grant, null, statuses, request.Search, request.Page, request.PageSize, tracked: false),
            cancellationToken);

        int total = await tickets.CountAsync(
            new PortalTicketsSpecification(grant, null, statuses, request.Search, null, null, tracked: false), cancellationToken);

        return Result.Success(new PagedResult<PortalTicketSummaryDto>(
            await mapper.ToSummariesAsync(page, cancellationToken), total, request.Page, request.PageSize));
    }
}
