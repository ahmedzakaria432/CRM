using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Organisation.Application.Theming;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.ChangeBranding;

public sealed class ChangeBrandingCommandHandler(
    IRepository<Domain.Organisation, TenantId> repository,
    IRepository<BrandLogo, BrandLogoId> logos,
    IOrganisationUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILocalizationService localization) : IRequestHandler<ChangeBrandingCommand, Result<BrandingSettingsDto>>
{
    public async Task<Result<BrandingSettingsDto>> Handle(ChangeBrandingCommand request, CancellationToken cancellationToken)
    {
        TenantId tenant = currentUser.Scope.Tenant;
        Domain.Organisation? organisation = await repository.GetByIdAsync(tenant, cancellationToken);
        if (organisation is null)
        {
            return Result.Failure<BrandingSettingsDto>(OrganisationErrors.OrganisationNotFound());
        }

        repository.SetOriginalRowVersion(organisation, request.RowVersion);

        var primary = BrandColour.Create(request.PrimaryColour);
        var accent = BrandColour.Create(request.AccentColour);
        if (BrandingRefusals.ContrastTooLow(BrandPalette.Evaluate(primary, accent), localization) is { } refusal)
        {
            return Result.Failure<BrandingSettingsDto>(refusal);
        }

        BrandLogoId? light = null;
        BrandLogoId? dark = null;
        foreach ((Guid? id, bool isLight) in new[] { (request.LightLogoId, true), (request.DarkLogoId, false) })
        {
            if (id is not { } logoId)
            {
                continue;
            }

            // A logo of another tenant is reported as not found (D4), never as a distinct forbidden.
            BrandLogo? logo = await logos.GetByIdAsync(new BrandLogoId(logoId), cancellationToken);
            if (logo is null || !logo.BelongsTo(tenant))
            {
                return Result.Failure<BrandingSettingsDto>(OrganisationErrors.BrandLogoNotFound(logoId));
            }

            if (isLight)
            {
                light = logo.Id;
            }
            else
            {
                dark = logo.Id;
            }
        }

        organisation.ChangeBranding(Branding.Create(
            LocalizedText.Create(request.DisplayNameAr, request.DisplayNameEn),
            BrandPalette.Create(primary, accent),
            Enum.Parse<ArabicFont>(request.ArabicFont),
            light,
            dark));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(BrandingSettingsDto.FromDomain(organisation));
    }
}
