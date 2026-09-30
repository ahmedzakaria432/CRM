using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetTicketById;

[RequirePermission(CrmPermissions.TicketsRead)]
public sealed record GetTicketByIdQuery(Guid Id) : IRequest<Result<TicketDto>>;
