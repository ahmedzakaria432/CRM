using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Organisation.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Organisation.Application.Queries.PreviewBranding;

public sealed class PreviewBrandingQueryValidator : AbstractValidator<PreviewBrandingQuery>
{
    public PreviewBrandingQueryValidator(ILocalizationService localization)
    {
        RuleFor(q => q.PrimaryColour).NotEmpty().HexColour(localization);
        RuleFor(q => q.AccentColour).NotEmpty().HexColour(localization);
    }
}
