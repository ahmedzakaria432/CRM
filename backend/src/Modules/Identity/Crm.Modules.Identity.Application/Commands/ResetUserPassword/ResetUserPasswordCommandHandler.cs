using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Commands.ResetUserPassword;

public sealed class ResetUserPasswordCommandHandler(
    IRepository<User, UserId> users,
    IPasswordHasher hasher,
    ICurrentUserService currentUser,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<ResetUserPasswordCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
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

        DateTimeOffset now = dateTime.UtcNow;

        users.SetOriginalRowVersion(user, request.RowVersion);
        user.ResetPassword(hasher.Hash(request.NewPassword), now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(UserDto.FromDomain(user, now));
    }
}
