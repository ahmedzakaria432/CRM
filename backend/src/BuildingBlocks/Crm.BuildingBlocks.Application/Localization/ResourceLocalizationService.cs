using System.Globalization;

namespace Crm.BuildingBlocks.Application.Localization;

public sealed class ResourceLocalizationService(IEnumerable<LocalizationResource> resources) : ILocalizationService
{
    private readonly LocalizationResource[] _resources = [.. resources];

    public string CurrentLanguage =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == CrmCultures.Arabic ? CrmCultures.Arabic : CrmCultures.English;

    public string Get(string key)
    {
        foreach (LocalizationResource resource in _resources)
        {
            string? value = resource.Manager.GetString(key, CultureInfo.CurrentUICulture);
            if (value is not null)
            {
                return value;
            }
        }

        return key;
    }
}
