using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Paging;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListPortalTickets;

/// <summary>The caller own tickets (all of the customer for the account admin). Tab is open (default) or closed; Status
/// is one portal status inside the tab; Search matches number or subject. Story 47.</summary>
[RequirePermission(CrmPermissions.PortalTickets)]
public sealed record ListPortalTicketsQuery(string? Tab, string? Status, string? Search, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<PortalTicketSummaryDto>>>;
