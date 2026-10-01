using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetPublicBranding;

public sealed class GetPublicBrandingQueryHandler(IRepository<Domain.Organisation, TenantId> repository)
    : IRequestHandler<GetPublicBrandingQuery, Result<PublicBrandingDto>>
{
    public async Task<Result<PublicBrandingDto>> Handle(GetPublicBrandingQuery request, CancellationToken cancellationToken)
    {
        Domain.Organisation? organisation = await repository.FirstOrDefaultAsync(
            new OrganisationByCodeSpecification(OrganisationCode.Create(request.Organisation)), cancellationToken);

        return organisation is null || !organisation.IsActive
            ? Result.Failure<PublicBrandingDto>(OrganisationErrors.OrganisationNotFound())
            : Result.Success(PublicBrandingDto.FromDomain(organisation));
    }
}
