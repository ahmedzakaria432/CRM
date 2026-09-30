using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Commands.ChangePortalPassword;

public sealed class ChangePortalPasswordCommandHandler(
    IRepository<PortalAccount, PortalAccountId> accounts,
    IPasswordHasher hasher,
    ICurrentUserService currentUser,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<ChangePortalPasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePortalPasswordCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out Guid accountId) || currentUser.Scope.PortalCustomer is not { } grant)
        {
            return Result.Failure(IdentityErrors.PortalOnly());
        }

        PortalAccount? account = await accounts.GetByIdAsync(new PortalAccountId(accountId), cancellationToken);

        if (account is null || account.ContactId != grant.ContactId)
        {
            return Result.Failure(IdentityErrors.PortalOnly());
        }

        if (!hasher.Verify(account.PasswordHash, request.CurrentPassword))
        {
            // The "Field: message" shape ValidationErrorParser reads, so the form shows it on the field.
            return Result.Failure(Error.Validation(
                "Identity.CurrentPasswordInvalid", "CurrentPassword: The current password is incorrect."));
        }

        Guid? keepSession = Guid.TryParse(currentUser.SessionId, out Guid sessionId) ? sessionId : null;
        account.ChangePassword(hasher.Hash(request.NewPassword), keepSession, dateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
