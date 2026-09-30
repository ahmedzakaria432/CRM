namespace Crm.Modules.Identity.Application.Localization;

/// <summary>Every resource key in <c>Resources/IdentityMessages*.resx</c>. Callers pass these constants to
/// <c>ILocalizationService.Get</c>; a key is never written as a string literal at a call site.</summary>
public static class IdentityMessageKeys
{
    public static class Validation
    {
        public const string PasswordNeedsLetter = "Identity.Validation.PasswordNeedsLetter";
        public const string PasswordNeedsDigit = "Identity.Validation.PasswordNeedsDigit";
        public const string LocaleUnsupported = "Identity.Validation.LocaleUnsupported";
    }
}
