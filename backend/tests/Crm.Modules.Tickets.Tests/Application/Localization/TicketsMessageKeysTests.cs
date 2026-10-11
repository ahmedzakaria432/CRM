using System.Globalization;
using System.Reflection;
using System.Resources;
using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Localization;

public sealed class TicketsMessageKeysTests
{
    private static List<string> AllKeys()
    {
        List<string> keys = [];
        foreach (Type nested in typeof(TicketsMessageKeys).GetNestedTypes(BindingFlags.Public))
        {
            keys.AddRange(nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => f.GetRawConstantValue() as string ?? string.Empty));
        }

        return keys;
    }

    [Fact]
    public void ThereAreTwentyTwoKeysInFourAreas()
    {
        Assert.Equal(22, AllKeys().Count);
        Assert.Equal(4, typeof(TicketsMessageKeys).GetNestedTypes(BindingFlags.Public).Length);
        Assert.Equal(8, typeof(TicketsMessageKeys.Validation).GetFields().Length);
        Assert.Equal(2, typeof(TicketsMessageKeys.Refusal).GetFields().Length);
        Assert.Equal(7, typeof(TicketsMessageKeys.Status).GetFields().Length);
        Assert.Equal(5, typeof(TicketsMessageKeys.PortalStatus).GetFields().Length);
    }

    [Fact]
    public void EveryKeyIsNonEmptyUniqueAndPrefixed()
    {
        List<string> keys = AllKeys();

        Assert.All(keys, key => Assert.StartsWith("Tickets.", key, StringComparison.Ordinal));
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.DoesNotContain(string.Empty, keys);
    }

    [Fact]
    public void EveryKeyResolvesToANonEmptyEnglishAndArabicValueThatDiffer()
    {
        ResourceManager manager = FakeLocalization.Manager();

        foreach (string key in AllKeys())
        {
            string? english = manager.GetString(key, CultureInfo.GetCultureInfo("en"));
            string? arabic = manager.GetString(key, CultureInfo.GetCultureInfo("ar"));

            Assert.False(string.IsNullOrWhiteSpace(english), key);
            Assert.False(string.IsNullOrWhiteSpace(arabic), key);
            Assert.NotEqual(english, arabic);
        }
    }

    [Fact]
    public void TheRefusalMessagesKeepTheirPlaceholderInBothLanguages()
    {
        ResourceManager manager = FakeLocalization.Manager();

        foreach (string key in new[] { TicketsMessageKeys.Refusal.TransitionNotAllowed, TicketsMessageKeys.Refusal.AlreadyClaimed })
        {
            Assert.Contains("{0}", manager.GetString(key, CultureInfo.GetCultureInfo("en")), StringComparison.Ordinal);
            Assert.Contains("{0}", manager.GetString(key, CultureInfo.GetCultureInfo("ar")), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheConstantCountEqualsTheEntriesOfBothResourceFiles()
    {
        ResourceManager manager = FakeLocalization.Manager();

        ResourceSet? neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, true, false);
        ResourceSet? arabic = manager.GetResourceSet(CultureInfo.GetCultureInfo("ar"), true, false);

        Assert.NotNull(neutral);
        Assert.NotNull(arabic);
        Assert.Equal(AllKeys().Count, neutral.Cast<System.Collections.DictionaryEntry>().Count());
        Assert.Equal(AllKeys().Count, arabic.Cast<System.Collections.DictionaryEntry>().Count());
    }

    [Fact]
    public void TheServiceResolvesAKeyInEachLanguage()
    {
        var localization = FakeLocalization.Resources();

        using (UiCulture.Use("en"))
        {
            Assert.Equal("Pending customer", localization.Get(TicketsMessageKeys.Status.PendingCustomer));
        }

        using (UiCulture.Use("ar"))
        {
            Assert.Equal("بانتظار العميل", localization.Get(TicketsMessageKeys.Status.PendingCustomer));
        }
    }
}
