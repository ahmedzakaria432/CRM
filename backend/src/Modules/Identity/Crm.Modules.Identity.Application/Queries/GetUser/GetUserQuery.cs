using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Queries.GetUser;

[RequirePermission(CrmPermissions.AdminUsers)]
public sealed record GetUserQuery(Guid Id) : IRequest<Result<UserDto>>;
