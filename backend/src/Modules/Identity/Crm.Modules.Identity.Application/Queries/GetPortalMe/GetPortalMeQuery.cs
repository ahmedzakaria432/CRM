using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Queries.GetPortalMe;

[RequirePermission(CrmPermissions.PortalAccount)]
public sealed record GetPortalMeQuery : IRequest<Result<PortalMeDto>>;
