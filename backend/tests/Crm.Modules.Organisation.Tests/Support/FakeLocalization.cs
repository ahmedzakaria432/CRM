using System.Resources;
using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Organisation.Application;

namespace Crm.Modules.Organisation.Tests.Support;

/// <summary>Localization doubles: the real resource pair, or the key itself.</summary>
public static class FakeLocalization
{
    public const string ResourceName = "Crm.Modules.Organisation.Application.Localization.Resources.OrganisationMessages";

    public static ResourceManager Manager() => new(ResourceName, typeof(OrganisationErrors).Assembly);

    /// <summary>The real OrganisationMessages resource pair through <see cref="ResourceLocalizationService"/>:
    /// the language follows the current UI culture at call time.</summary>
    public static ILocalizationService Resources() => new ResourceLocalizationService([new LocalizationResource(Manager())]);

    /// <summary>Returns the key itself.</summary>
    public static ILocalizationService Keys() => new KeyLocalization();

    private sealed class KeyLocalization : ILocalizationService
    {
        public string CurrentLanguage => "en";

        public string Get(string key) => key;
    }
}
