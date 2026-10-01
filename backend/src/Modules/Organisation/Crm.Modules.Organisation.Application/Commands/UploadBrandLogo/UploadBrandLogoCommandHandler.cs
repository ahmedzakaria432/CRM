using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Theming;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.UploadBrandLogo;

public sealed class UploadBrandLogoCommandHandler(
    IRepository<BrandLogo, BrandLogoId> repository,
    IOrganisationUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ISvgSanitizer sanitizer) : IRequestHandler<UploadBrandLogoCommand, Result<BrandLogoDto>>
{
    public async Task<Result<BrandLogoDto>> Handle(UploadBrandLogoCommand request, CancellationToken cancellationToken)
    {
        byte[] content = request.Content;

        // Sniff throws LogoEmpty / LogoTooLarge / LogoTypeNotAllowed (422): the domain is the authority on size and type.
        if (BrandLogo.Sniff(content) == LogoContentType.Svg)
        {
            byte[]? clean = sanitizer.Sanitize(content);
            if (clean is null)
            {
                return Result.Failure<BrandLogoDto>(OrganisationErrors.LogoTypeNotAllowed());
            }

            content = clean;
        }

        BrandLogo logo = BrandLogo.Create(currentUser.Scope.Tenant, content);
        repository.Add(logo);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BrandLogoDto(logo.Id.Value, logo.MediaType, logo.Content.Length));
    }
}
