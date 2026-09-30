using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetPortalTicketThread;

/// <summary>The public messages of one of the caller own tickets, oldest first (Story 47). Notes and activities are
/// structurally unreachable: the handler names only ITicketThreadReadModel.ListPublicAsync.</summary>
[RequirePermission(CrmPermissions.PortalTickets)]
public sealed record GetPortalTicketThreadQuery(Guid Id, int? After, int Limit = 50) : IRequest<Result<PortalThreadPageDto>>;
