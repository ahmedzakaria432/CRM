using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Queries.GetStaffUsers;

public sealed class GetStaffUsersQueryHandler(IRepository<User, UserId> users, ICurrentUserService currentUser)
    : IRequestHandler<GetStaffUsersQuery, Result<IReadOnlyList<StaffUserSummary>>>
{
    public async Task<Result<IReadOnlyList<StaffUserSummary>>> Handle(GetStaffUsersQuery request, CancellationToken cancellationToken)
    {
        if (request.UserIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<StaffUserSummary>>([]);
        }

        IReadOnlyList<User> found = await users.ListAsync(
            new UsersByIdsSpecification(currentUser.Scope.Tenant, [.. request.UserIds.Distinct().Select(id => new UserId(id))]),
            cancellationToken);

        return Result.Success<IReadOnlyList<StaffUserSummary>>(
            [.. found.Select(u => new StaffUserSummary(u.Id.Value, u.DisplayName.Value, u.IsActive))]);
    }
}
