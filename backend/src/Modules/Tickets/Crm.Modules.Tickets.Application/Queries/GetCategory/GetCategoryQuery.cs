using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetCategory;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record GetCategoryQuery(Guid Id) : IRequest<Result<TicketCategoryDto>>;
