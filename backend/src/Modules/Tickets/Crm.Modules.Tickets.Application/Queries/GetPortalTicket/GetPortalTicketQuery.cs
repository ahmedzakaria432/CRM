using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetPortalTicket;

/// <summary>One of the caller own tickets. Anything else (unknown, another customer, a colleague of a non-admin
/// contact) is 404, never 403 (Story 47, D11).</summary>
[RequirePermission(CrmPermissions.PortalTickets)]
public sealed record GetPortalTicketQuery(Guid Id) : IRequest<Result<PortalTicketDto>>;
