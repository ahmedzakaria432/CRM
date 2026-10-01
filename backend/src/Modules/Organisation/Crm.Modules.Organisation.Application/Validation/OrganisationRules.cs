using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Organisation.Application.Localization;
using Crm.Modules.Organisation.Domain;
using FluentValidation;

namespace Crm.Modules.Organisation.Application.Validation;

public static class OrganisationRules
{
    /// <summary>A colour is #RRGGBB.</summary>
    public static IRuleBuilderOptions<T, string> HexColour<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Matches("^#[0-9A-Fa-f]{6}$")
            .WithMessage(_ => localization.Get(OrganisationMessageKeys.Validation.ColourInvalid));
    }

    /// <summary>The Arabic font is one of the ArabicFont names (numbers are rejected).</summary>
    public static IRuleBuilderOptions<T, string> SupportedArabicFont<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(f => Enum.GetNames<ArabicFont>().Contains(f, StringComparer.Ordinal))
            .WithMessage(_ => localization.Get(OrganisationMessageKeys.Validation.ArabicFontUnsupported));
    }

    /// <summary>An organisation code is 2-20 characters of letters, digits or hyphen.</summary>
    public static IRuleBuilderOptions<T, string> OrganisationCode<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Matches("^[A-Za-z0-9-]{2,20}$")
            .WithMessage(_ => localization.Get(OrganisationMessageKeys.Validation.OrganisationCodeInvalid));
    }
}
