using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Identity.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.PreferredLocale).SupportedLocale(localization);
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
