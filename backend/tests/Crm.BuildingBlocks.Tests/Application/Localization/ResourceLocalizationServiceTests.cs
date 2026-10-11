using System.Resources;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Tests.Support;

namespace Crm.BuildingBlocks.Tests.Application.Localization;

public sealed class ResourceLocalizationServiceTests
{
    private static readonly ResourceManager RealResources = new(
        "Crm.BuildingBlocks.Application.Localization.Resources.CrmMessages", typeof(CrmCultures).Assembly);

    private static ResourceLocalizationService Real() => new([new LocalizationResource(RealResources)]);

    [Fact]
    public void GetReturnsDifferentRealStringsForEnglishAndArabic()
    {
        var service = Real();
        string english;
        string arabic;

        using (UiCulture.Use("en"))
        {
            english = service.Get(CrmMessageKeys.ProblemTitle.Validation);
        }

        using (UiCulture.Use("ar"))
        {
            arabic = service.Get(CrmMessageKeys.ProblemTitle.Validation);
        }

        Assert.NotEqual(CrmMessageKeys.ProblemTitle.Validation, english);
        Assert.NotEqual(CrmMessageKeys.ProblemTitle.Validation, arabic);
        Assert.NotEqual(english, arabic);
    }

    [Fact]
    public void AnUnknownKeyReturnsTheKeyItself()
    {
        using var culture = UiCulture.Use("en");

        Assert.Equal("Some.Missing.Key", Real().Get("Some.Missing.Key"));
    }

    [Fact]
    public void TheFirstResourceThatHasTheKeyWins()
    {
        var first = new StubResourceManager(new Dictionary<string, string> { ["k"] = "from first" });
        var second = new StubResourceManager(new Dictionary<string, string> { ["k"] = "from second", ["only"] = "only second" });
        var service = new ResourceLocalizationService([new LocalizationResource(first), new LocalizationResource(second)]);

        Assert.Equal("from first", service.Get("k"));
        Assert.Equal("only second", service.Get("only"));
    }

    [Fact]
    public void NoResourcesAtAllReturnsTheKey()
    {
        Assert.Equal("k", new ResourceLocalizationService([]).Get("k"));
    }

    [Theory]
    [InlineData("ar", "ar")]
    [InlineData("ar-SA", "ar")]
    [InlineData("ar-EG", "ar")]
    [InlineData("en", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("fr", "en")]
    public void CurrentLanguageIsArabicForArabicCulturesAndEnglishOtherwise(string culture, string expected)
    {
        using var scope = UiCulture.Use(culture);

        Assert.Equal(expected, Real().CurrentLanguage);
    }

    [Fact]
    public void CurrentLanguageForTheInvariantCultureIsEnglish()
    {
        using var scope = UiCulture.Use(string.Empty);

        Assert.Equal("en", Real().CurrentLanguage);
    }

    [Fact]
    public void TheCultureIsReadAtCallTimeSoOneInstanceServesBothLanguages()
    {
        var service = Real();

        using (UiCulture.Use("ar"))
        {
            Assert.Equal("ar", service.CurrentLanguage);
        }

        using (UiCulture.Use("en"))
        {
            Assert.Equal("en", service.CurrentLanguage);
        }
    }

    [Fact]
    public void CrmCulturesListsEnglishThenArabicWithEnglishAsDefault()
    {
        Assert.Equal(["en", "ar"], CrmCultures.All);
        Assert.Equal("en", CrmCultures.Default);
        Assert.Equal("en", CrmCultures.English);
        Assert.Equal("ar", CrmCultures.Arabic);
    }

    [Fact]
    public void LocalizationResourceHasValueEquality()
    {
        Assert.Equal(new LocalizationResource(RealResources), new LocalizationResource(RealResources));
    }
}
