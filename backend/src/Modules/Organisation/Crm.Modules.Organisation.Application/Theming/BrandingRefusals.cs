using System.Globalization;
using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Organisation.Application.Localization;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Application.Theming;

/// <summary>Asks the palette rules BEFORE the domain call, so a failing palette is a 422 whose detail names the check,
/// the ratio and the requirement in the request language. The domain throws the same code (English) as the backstop.
/// Numbers use InvariantCulture on purpose (Latin digits; the frontend wraps them in a left-to-right isolate).</summary>
internal static class BrandingRefusals
{
    public static Error? ContrastTooLow(IReadOnlyList<ContrastCheck> checks, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(localization);

        ContrastCheck? failing = checks.FirstOrDefault(c => !c.Passes);
        return failing is null
            ? null
            : OrganisationErrors.ContrastTooLow(string.Format(
                CultureInfo.CurrentCulture,
                localization.Get(OrganisationMessageKeys.Refusal.ContrastTooLow),
                CheckName(failing.Name, localization),
                failing.Ratio.ToString("0.00", CultureInfo.InvariantCulture),
                failing.Required.ToString("0.0", CultureInfo.InvariantCulture)));
    }

    public static string CheckName(string name, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);

        return localization.Get(name switch
        {
            ContrastCheck.Names.PrimaryText => OrganisationMessageKeys.Contrast.PrimaryText,
            _ => OrganisationMessageKeys.Contrast.AccentOnSurface,
        });
    }
}
