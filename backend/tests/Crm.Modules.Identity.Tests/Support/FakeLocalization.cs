using System.Resources;
using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Identity.Application;

namespace Crm.Modules.Identity.Tests.Support;

/// <summary>Localization doubles: the real resource pair, or the key itself.</summary>
public static class FakeLocalization
{
    public const string ResourceName = "Crm.Modules.Identity.Application.Localization.Resources.IdentityMessages";

    public static ResourceManager Manager() => new(ResourceName, typeof(SessionSettings).Assembly);

    public static ILocalizationService Resources() => new ResourceLocalizationService([new LocalizationResource(Manager())]);

    public static ILocalizationService Keys() => new KeyLocalization();

    private sealed class KeyLocalization : ILocalizationService
    {
        public string CurrentLanguage => "en";

        public string Get(string key) => key;
    }
}
