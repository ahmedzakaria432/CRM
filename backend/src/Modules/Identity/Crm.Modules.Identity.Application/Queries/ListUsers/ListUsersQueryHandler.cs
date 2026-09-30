using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Paging;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Queries.ListUsers;

public sealed class ListUsersQueryHandler(
    IRepository<User, UserId> users,
    ICurrentUserService currentUser,
    IDateTime dateTime)
    : IRequestHandler<ListUsersQuery, Result<PagedResult<UserDto>>>
{
    public async Task<Result<PagedResult<UserDto>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<User> page = await users.ListAsync(
            new UsersSpecification(currentUser.Scope.Tenant, request.Search, request.Page, request.PageSize), cancellationToken);

        int total = await users.CountAsync(
            new UsersSpecification(currentUser.Scope.Tenant, request.Search, null, null), cancellationToken);

        DateTimeOffset now = dateTime.UtcNow;

        return Result.Success(new PagedResult<UserDto>(
            [.. page.Select(u => UserDto.FromDomain(u, now))], total, request.Page, request.PageSize));
    }
}
