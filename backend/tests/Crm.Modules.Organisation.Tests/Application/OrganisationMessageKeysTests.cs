using System.Globalization;
using System.Reflection;
using System.Resources;
using Crm.Modules.Organisation.Application.Localization;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application;

public sealed class OrganisationMessageKeysTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar");

    public static TheoryData<string> AllKeys()
    {
        var keys = new TheoryData<string>();
        foreach (Type nested in typeof(OrganisationMessageKeys).GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (FieldInfo field in nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (field.IsLiteral && field.GetRawConstantValue() is string key)
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    [Fact]
    public void TheKeysClassDeclaresSixKeys()
    {
        Assert.Equal(6, AllKeys().Count);
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void EveryKeyHasANonEmptyEnglishValue(string key)
    {
        string? value = FakeLocalization.Manager().GetString(key, English);

        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void EveryKeyHasANonEmptyArabicValueThatDiffersFromEnglish(string key)
    {
        ResourceManager manager = FakeLocalization.Manager();

        string? arabic = manager.GetString(key, Arabic);

        Assert.False(string.IsNullOrWhiteSpace(arabic));
        Assert.NotEqual(manager.GetString(key, English), arabic);
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void EveryKeyStartsWithTheModuleName(string key)
    {
        Assert.StartsWith("Organisation.", key, StringComparison.Ordinal);
    }

    [Fact]
    public void TheContrastTemplateTakesThreeArgumentsInBothLanguages()
    {
        ResourceManager manager = FakeLocalization.Manager();

        foreach (CultureInfo culture in new[] { English, Arabic })
        {
            string template = manager.GetString(OrganisationMessageKeys.Refusal.ContrastTooLow, culture) ?? string.Empty;

            Assert.Contains("{0}", template, StringComparison.Ordinal);
            Assert.Contains("{1}", template, StringComparison.Ordinal);
            Assert.Contains("{2}", template, StringComparison.Ordinal);
        }
    }
}
