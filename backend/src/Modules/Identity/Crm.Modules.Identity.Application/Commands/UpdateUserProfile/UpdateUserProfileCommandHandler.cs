using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileCommandHandler(
    IRepository<User, UserId> users,
    ICurrentUserService currentUser,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<UpdateUserProfileCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
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

        users.SetOriginalRowVersion(user, request.RowVersion);
        user.Rename(DisplayName.Create(request.DisplayName));
        user.ChangePreferredLocale(PreferredLocale.Create(request.PreferredLocale));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(UserDto.FromDomain(user, dateTime.UtcNow));
    }
}
