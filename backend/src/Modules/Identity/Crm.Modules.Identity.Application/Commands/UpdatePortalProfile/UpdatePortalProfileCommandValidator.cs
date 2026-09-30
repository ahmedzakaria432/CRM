using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Identity.Application.Validation;
using Crm.Modules.Identity.Domain;
using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.UpdatePortalProfile;

public sealed class UpdatePortalProfileCommandValidator : AbstractValidator<UpdatePortalProfileCommand>
{
    public UpdatePortalProfileCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(DisplayName.MaxLength);
        RuleFor(c => c.PreferredLocale).SupportedLocale(localization);
    }
}
