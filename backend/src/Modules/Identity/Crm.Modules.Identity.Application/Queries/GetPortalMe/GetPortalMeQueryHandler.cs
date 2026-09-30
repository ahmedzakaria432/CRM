using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Queries.GetPortalMe;

public sealed class GetPortalMeQueryHandler(IRepository<PortalAccount, PortalAccountId> accounts, ICurrentUserService currentUser)
    : IRequestHandler<GetPortalMeQuery, Result<PortalMeDto>>
{
    public async Task<Result<PortalMeDto>> Handle(GetPortalMeQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out Guid accountId))
        {
            return Result.Failure<PortalMeDto>(IdentityErrors.PortalOnly());
        }

        PortalAccount? account = await accounts.GetByIdAsync(new PortalAccountId(accountId), cancellationToken);

        return PortalMeMapper.ToMe(account, currentUser.Scope);
    }
}
