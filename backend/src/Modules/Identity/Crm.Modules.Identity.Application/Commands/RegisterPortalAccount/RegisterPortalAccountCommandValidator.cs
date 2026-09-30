using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Identity.Application.Localization;
using Crm.Modules.Identity.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.RegisterPortalAccount;

public sealed class RegisterPortalAccountCommandValidator : AbstractValidator<RegisterPortalAccountCommand>
{
    public RegisterPortalAccountCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(254);
        RuleFor(c => c.Password).StaffPassword(localization);
        RuleFor(c => c.PreferredLocale)
            .Must(l => l is null or "en" or "ar")
            .WithMessage(_ => localization.Get(IdentityMessageKeys.Validation.LocaleUnsupported));
    }
}
