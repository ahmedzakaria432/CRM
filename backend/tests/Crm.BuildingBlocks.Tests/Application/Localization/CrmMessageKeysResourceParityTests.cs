using System.Reflection;
using System.Resources;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Tests.Support;

namespace Crm.BuildingBlocks.Tests.Application.Localization;

/// <summary>A key added to <see cref="CrmMessageKeys"/> without an entry in both resx files fails here. The Arabic
/// text is never written in test source: the assertion is "present, non-empty and not the key".</summary>
public sealed class CrmMessageKeysResourceParityTests
{
    private static readonly ResourceManager Resources = new(
        "Crm.BuildingBlocks.Application.Localization.Resources.CrmMessages", typeof(CrmCultures).Assembly);

    public static TheoryData<string, string> EveryKey()
    {
        var data = new TheoryData<string, string>();

        foreach ((string group, string key) in Keys())
        {
            data.Add(group, key);
        }

        return data;
    }

    private static List<(string Group, string Key)> Keys()
    {
        List<(string Group, string Key)> keys = [];

        foreach (Type group in new[] { typeof(CrmMessageKeys.ProblemTitle), typeof(CrmMessageKeys.ProblemDetail) })
        {
            foreach (FieldInfo field in group.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (field.IsLiteral && field.GetRawConstantValue() is string key)
                {
                    keys.Add((group.Name, key));
                }
            }
        }

        return keys;
    }

    [Theory]
    [MemberData(nameof(EveryKey))]
    public void EveryKeyHasAnEnglishAndAnArabicValue(string group, string key)
    {
        using (UiCulture.Use("en"))
        {
            var english = Resources.GetString(key, System.Globalization.CultureInfo.CurrentUICulture);
            Assert.False(string.IsNullOrWhiteSpace(english), $"{group}: '{key}' has no English value");
            Assert.NotEqual(key, english);
        }

        using (UiCulture.Use("ar"))
        {
            var arabic = Resources.GetString(key, System.Globalization.CultureInfo.CurrentUICulture);
            Assert.False(string.IsNullOrWhiteSpace(arabic), $"{group}: '{key}' has no Arabic value");
            Assert.NotEqual(key, arabic);
        }
    }

    [Fact]
    public void KeysAreUniqueAcrossBothGroups()
    {
        var keys = Keys().Select(k => k.Key).ToList();

        Assert.NotEmpty(keys);
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ThereIsOneTitleKeyPerProblemTypeAndTheEightDetailKeys()
    {
        var rows = Keys().Select(k => k.Group).ToList();

        Assert.Equal(12, rows.Count(g => g == nameof(CrmMessageKeys.ProblemTitle)));
        Assert.Equal(8, rows.Count(g => g == nameof(CrmMessageKeys.ProblemDetail)));
    }

    [Fact]
    public void ArabicDiffersFromEnglishForTheValidationTitle()
    {
        string english;
        string arabic;

        using (UiCulture.Use("en"))
        {
            english = Resources.GetString(CrmMessageKeys.ProblemTitle.Validation, System.Globalization.CultureInfo.CurrentUICulture) ?? string.Empty;
        }

        using (UiCulture.Use("ar"))
        {
            arabic = Resources.GetString(CrmMessageKeys.ProblemTitle.Validation, System.Globalization.CultureInfo.CurrentUICulture) ?? string.Empty;
        }

        Assert.NotEqual(english, arabic);
    }
}
