using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Commands.DeactivateUser;

public sealed class DeactivateUserCommandHandler(
    IRepository<User, UserId> users,
    ICurrentUserService currentUser,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateUserCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
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

        // Cross-aggregate fact (the caller is the target), so it belongs to the handler, not to User.
        if (string.Equals(request.Id.ToString(), currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<UserDto>(Error.Domain("Identity.CannotDeactivateSelf", "You cannot deactivate your own account."));
        }

        DateTimeOffset now = dateTime.UtcNow;

        users.SetOriginalRowVersion(user, request.RowVersion);
        user.Deactivate(now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(UserDto.FromDomain(user, now));
    }
}
