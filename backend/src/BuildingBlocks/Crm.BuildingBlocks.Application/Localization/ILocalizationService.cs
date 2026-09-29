namespace Crm.BuildingBlocks.Application.Localization;

/// <summary>Server-side text in the request's language (Accept-Language, set by UseCrmRequestLocalization).
/// Reads CultureInfo.CurrentUICulture at call time, so it is a singleton.</summary>
public interface ILocalizationService
{
    /// <summary>"en" or "ar".</summary>
    string CurrentLanguage { get; }

    /// <summary>The resource value for <paramref name="key"/> in the current UI culture, falling back to the
    /// neutral (English) resource, then to the key itself so a missing entry is visible, never an exception.</summary>
    string Get(string key);
}
