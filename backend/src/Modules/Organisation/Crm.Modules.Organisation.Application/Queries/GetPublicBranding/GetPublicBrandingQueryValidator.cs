using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Organisation.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Organisation.Application.Queries.GetPublicBranding;

public sealed class GetPublicBrandingQueryValidator : AbstractValidator<GetPublicBrandingQuery>
{
    public GetPublicBrandingQueryValidator(ILocalizationService localization)
    {
        RuleFor(q => q.Organisation).NotEmpty().OrganisationCode(localization);
    }
}
