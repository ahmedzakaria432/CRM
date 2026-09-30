using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler(
    IRepository<User, UserId> users,
    IPasswordHasher hasher,
    ICurrentUserService currentUser,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out Guid userId))
        {
            return Result.Failure(IdentityErrors.Unauthenticated());
        }

        User? user = await users.GetByIdAsync(new UserId(userId), cancellationToken);

        if (user is null)
        {
            return Result.Failure(IdentityErrors.Unauthenticated());
        }

        if (!hasher.Verify(user.PasswordHash, request.CurrentPassword))
        {
            // The "Field: message" shape ValidationErrorParser reads, so the form shows it on the field.
            return Result.Failure(Error.Validation(
                "Identity.CurrentPasswordInvalid", "CurrentPassword: The current password is incorrect."));
        }

        Guid? keepSession = Guid.TryParse(currentUser.SessionId, out Guid sessionId) ? sessionId : null;
        user.ChangePassword(hasher.Hash(request.NewPassword), keepSession, dateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
