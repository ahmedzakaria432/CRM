using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Commands.UpdatePortalProfile;

public sealed class UpdatePortalProfileCommandHandler(
    IRepository<PortalAccount, PortalAccountId> accounts,
    ICurrentUserService currentUser,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<UpdatePortalProfileCommand, Result<PortalMeDto>>
{
    public async Task<Result<PortalMeDto>> Handle(UpdatePortalProfileCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out Guid accountId))
        {
            return Result.Failure<PortalMeDto>(IdentityErrors.PortalOnly());
        }

        PortalAccount? account = await accounts.GetByIdAsync(new PortalAccountId(accountId), cancellationToken);
        Result<PortalMeDto> me = PortalMeMapper.ToMe(account, currentUser.Scope);

        if (me.IsFailure || account is null)
        {
            return me;
        }

        account.UpdateProfile(DisplayName.Create(request.DisplayName), PreferredLocale.Create(request.PreferredLocale));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return PortalMeMapper.ToMe(account, currentUser.Scope);
    }
}
