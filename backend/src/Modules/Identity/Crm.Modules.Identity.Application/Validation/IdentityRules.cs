using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Identity.Application.Localization;
using FluentValidation;

namespace Crm.Modules.Identity.Application.Validation;

public static class IdentityRules
{
    /// <summary>10-128 characters, at least one letter and one digit. Messages follow Accept-Language.</summary>
    public static IRuleBuilderOptions<T, string> StaffPassword<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(128)
            .Matches("[A-Za-z]").WithMessage(_ => localization.Get(IdentityMessageKeys.Validation.PasswordNeedsLetter))
            .Matches("[0-9]").WithMessage(_ => localization.Get(IdentityMessageKeys.Validation.PasswordNeedsDigit));
    }

    /// <summary>The preferred language is en or ar.</summary>
    public static IRuleBuilderOptions<T, string> SupportedLocale<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(l => l is "en" or "ar")
            .WithMessage(_ => localization.Get(IdentityMessageKeys.Validation.LocaleUnsupported));
    }
}
