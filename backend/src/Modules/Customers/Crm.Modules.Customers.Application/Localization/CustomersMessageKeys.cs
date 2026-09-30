namespace Crm.Modules.Customers.Application.Localization;

/// <summary>Every resource key in <c>Resources/CustomersMessages*.resx</c>. Callers pass these constants to
/// <c>ILocalizationService.Get</c>; a key is never written as a string literal at a call site.</summary>
public static class CustomersMessageKeys
{
    public static class Validation
    {
        public const string NameRequired = "Customers.Validation.NameRequired";
        public const string LanguageUnsupported = "Customers.Validation.LanguageUnsupported";
        public const string SortUnsupported = "Customers.Validation.SortUnsupported";
    }
}
