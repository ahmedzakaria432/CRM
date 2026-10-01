using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetBrandLogo;

public sealed class GetBrandLogoQueryHandler(IRepository<BrandLogo, BrandLogoId> repository)
    : IRequestHandler<GetBrandLogoQuery, Result<BrandLogoContentDto>>
{
    public async Task<Result<BrandLogoContentDto>> Handle(GetBrandLogoQuery request, CancellationToken cancellationToken)
    {
        BrandLogo? logo = await repository.GetByIdAsync(new BrandLogoId(request.Id), cancellationToken);

        return logo is null
            ? Result.Failure<BrandLogoContentDto>(OrganisationErrors.BrandLogoNotFound(request.Id))
            : Result.Success(new BrandLogoContentDto(logo.Content, logo.MediaType));
    }
}
