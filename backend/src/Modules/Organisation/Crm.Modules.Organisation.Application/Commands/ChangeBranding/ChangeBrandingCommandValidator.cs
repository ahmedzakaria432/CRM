using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Organisation.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Organisation.Application.Commands.ChangeBranding;

public sealed class ChangeBrandingCommandValidator : AbstractValidator<ChangeBrandingCommand>
{
    public ChangeBrandingCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.DisplayNameAr).NotEmpty().MaximumLength(200);
        RuleFor(c => c.DisplayNameEn).NotEmpty().MaximumLength(200);
        RuleFor(c => c.PrimaryColour).NotEmpty().HexColour(localization);
        RuleFor(c => c.AccentColour).NotEmpty().HexColour(localization);
        RuleFor(c => c.ArabicFont).NotEmpty().SupportedArabicFont(localization);
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
