using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Paging;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Queries.ListUsers;

[RequirePermission(CrmPermissions.AdminUsers)]
public sealed record ListUsersQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<UserDto>>>;
