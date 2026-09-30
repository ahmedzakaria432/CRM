using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Queries.GetUser;

public sealed class GetUserQueryHandler(
    IRepository<User, UserId> users,
    ICurrentUserService currentUser,
    IDateTime dateTime)
    : IRequestHandler<GetUserQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(new UserId(request.Id), cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserDto>(IdentityErrors.UserNotFound(request.Id));
        }

        if (user.TenantId != currentUser.Scope.Tenant)
        {
            return Result.Failure<UserDto>(IdentityErrors.OutOfScope());
        }

        return Result.Success(UserDto.FromDomain(user, dateTime.UtcNow));
    }
}
