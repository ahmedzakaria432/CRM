using Crm.BuildingBlocks.Application.Abstractions;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetBrandingSettings;

public sealed class GetBrandingSettingsQueryHandler(IRepository<Domain.Organisation, TenantId> repository, ICurrentUserService currentUser)
    : IRequestHandler<GetBrandingSettingsQuery, Result<BrandingSettingsDto>>
{
    public async Task<Result<BrandingSettingsDto>> Handle(GetBrandingSettingsQuery request, CancellationToken cancellationToken)
    {
        Domain.Organisation? organisation = await repository.GetByIdAsync(currentUser.Scope.Tenant, cancellationToken);

        return organisation is null
            ? Result.Failure<BrandingSettingsDto>(OrganisationErrors.OrganisationNotFound())
            : Result.Success(BrandingSettingsDto.FromDomain(organisation));
    }
}
