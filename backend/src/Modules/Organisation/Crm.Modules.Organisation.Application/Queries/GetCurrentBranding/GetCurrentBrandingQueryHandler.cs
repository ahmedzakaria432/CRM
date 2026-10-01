using Crm.BuildingBlocks.Application.Abstractions;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetCurrentBranding;

public sealed class GetCurrentBrandingQueryHandler(IRepository<Domain.Organisation, TenantId> repository, ICurrentUserService currentUser)
    : IRequestHandler<GetCurrentBrandingQuery, Result<PublicBrandingDto>>
{
    public async Task<Result<PublicBrandingDto>> Handle(GetCurrentBrandingQuery request, CancellationToken cancellationToken)
    {
        Domain.Organisation? organisation = await repository.GetByIdAsync(currentUser.Scope.Tenant, cancellationToken);

        return organisation is null
            ? Result.Failure<PublicBrandingDto>(OrganisationErrors.OrganisationNotFound())
            : Result.Success(PublicBrandingDto.FromDomain(organisation));
    }
}
