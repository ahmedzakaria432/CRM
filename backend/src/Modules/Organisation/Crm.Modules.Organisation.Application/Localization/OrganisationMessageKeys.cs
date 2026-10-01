namespace Crm.Modules.Organisation.Application.Localization;

/// <summary>Every resource key in Resources/OrganisationMessages*.resx (Story 58). Callers pass these constants to
/// <c>ILocalizationService.Get</c>; a key is never written as a string literal at a call site.</summary>
public static class OrganisationMessageKeys
{
    public static class Validation
    {
        public const string ColourInvalid = "Organisation.Validation.ColourInvalid";
        public const string ArabicFontUnsupported = "Organisation.Validation.ArabicFontUnsupported";
        public const string OrganisationCodeInvalid = "Organisation.Validation.OrganisationCodeInvalid";
    }

    public static class Refusal
    {
        public const string ContrastTooLow = "Organisation.Refusal.ContrastTooLow";
    }

    public static class Contrast
    {
        public const string PrimaryText = "Organisation.Contrast.PrimaryText";
        public const string AccentOnSurface = "Organisation.Contrast.AccentOnSurface";
    }
}
