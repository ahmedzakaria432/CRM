using System.Resources;
using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Customers.Application;

namespace Crm.Modules.Customers.Tests.Support;

/// <summary>Localization doubles: the real resource pair, or the key itself.</summary>
public static class FakeLocalization
{
    public const string ResourceName = "Crm.Modules.Customers.Application.Localization.Resources.CustomersMessages";

    public static ResourceManager Manager() => new(ResourceName, typeof(CustomersCacheKeys).Assembly);

    public static ILocalizationService Resources() => new ResourceLocalizationService([new LocalizationResource(Manager())]);

    public static ILocalizationService Keys() => new KeyLocalization();

    private sealed class KeyLocalization : ILocalizationService
    {
        public string CurrentLanguage => "en";

        public string Get(string key) => key;
    }
}
