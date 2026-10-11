using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Theming;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Theming;

public sealed class BrandTokenGeneratorTests
{
    private static readonly string[] Steps = ["50", "100", "200", "300", "400", "500", "600", "700", "800", "900", "950"];
    private static readonly string[] Roles = ["primary", "accent"];

    // The 24-step bisection plus byte rounding stay within 1e-3 of the window for every sampled colour (plan risk R8).
    private const double Epsilon = 0.001;

    private static BrandColour Colour(string hex) => BrandColour.Create(hex);

    private static BrandTokensDto Generate(string primary, string accent = "#B7791F") => BrandTokenGenerator.Generate(Colour(primary), Colour(accent));

    public static TheoryData<string> SampleColours() =>
        ["#0E6655", "#B7791F", "#145EC1", "#000000", "#FFFFFF", "#767676", "#FF0000", "#00FF00", "#0000FF", "#FFD400", "#0B1F4B"];

    public static TheoryData<string> DarkPrimaries() => ["#0B1F4B", "#FFD400", "#000000", "#FFFFFF", "#0E6655"];

    [Fact]
    public void TheDefaultsAreTheProductColours()
    {
        Assert.Equal("#145EC1", BrandDefaults.Primary);
        Assert.Equal("#0F766E", BrandDefaults.Accent);
    }

    [Fact]
    public void TheDarkWindowConstantsAreTheDocumentedBounds()
    {
        Assert.Equal(0.115, BrandTokenGenerator.DarkMinLuminance);
        Assert.Equal(0.180, BrandTokenGenerator.DarkPrimaryMaxLuminance);
    }

    [Theory]
    [MemberData(nameof(SampleColours))]
    public void EveryThemeHasTwentyTwoKeysNamedByRoleAndStep(string primary)
    {
        BrandTokensDto tokens = Generate(primary);

        string[] expected = [.. Roles.SelectMany(role => Steps.Select(step => $"--crm-{role}-{step}"))];
        Assert.Equal(expected.Order(StringComparer.Ordinal), tokens.Light.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(expected.Order(StringComparer.Ordinal), tokens.Dark.Keys.Order(StringComparer.Ordinal));
        Assert.All(tokens.Light.Keys, key => Assert.Matches("^--crm-(primary|accent)-(50|100|200|300|400|500|600|700|800|900|950)$", key));
    }

    [Theory]
    [MemberData(nameof(SampleColours))]
    public void LightStepSixHundredIsTheInputHexExactly(string hex)
    {
        BrandTokensDto tokens = Generate(hex, hex);

        Assert.Equal(hex, tokens.Light["--crm-primary-600"]);
        Assert.Equal(hex, tokens.Light["--crm-accent-600"]);
    }

    [Theory]
    [MemberData(nameof(SampleColours))]
    public void EveryValueIsAnUpperCaseHexColour(string hex)
    {
        BrandTokensDto tokens = Generate(hex, hex);

        Assert.All(tokens.Light.Values, value => Assert.Matches("^#[0-9A-F]{6}$", value));
        Assert.All(tokens.Dark.Values, value => Assert.Matches("^#[0-9A-F]{6}$", value));
    }

    [Theory]
    [InlineData("#0E6655")]
    [InlineData("#B7791F")]
    [InlineData("#145EC1")]
    [InlineData("#0B1F4B")]
    public void TheLightScaleGetsDarkerFromFiftyToNineFifty(string hex)
    {
        BrandTokensDto tokens = Generate(hex, hex);

        double[] luminance = [.. Steps.Select(step => Colour(tokens.Light[$"--crm-primary-{step}"]).RelativeLuminance)];

        for (int i = 1; i < luminance.Length; i++)
        {
            Assert.True(luminance[i - 1] > luminance[i], $"step {Steps[i - 1]} ({luminance[i - 1]}) is not lighter than step {Steps[i]} ({luminance[i]})");
        }
    }

    [Fact]
    public void GenerationIsDeterministic()
    {
        BrandTokensDto first = Generate("#0E6655");
        BrandTokensDto second = Generate("#0E6655");

        Assert.Equal(first.Light.OrderBy(p => p.Key, StringComparer.Ordinal), second.Light.OrderBy(p => p.Key, StringComparer.Ordinal));
        Assert.Equal(first.Dark.OrderBy(p => p.Key, StringComparer.Ordinal), second.Dark.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(DarkPrimaries))]
    public void TheDarkPrimaryLandsInsideTheLuminanceWindow(string primary)
    {
        double luminance = Colour(Generate(primary).Dark["--crm-primary-600"]).RelativeLuminance;

        Assert.InRange(luminance, BrandTokenGenerator.DarkMinLuminance - Epsilon, BrandTokenGenerator.DarkPrimaryMaxLuminance + Epsilon);
    }

    [Theory]
    [MemberData(nameof(DarkPrimaries))]
    public void WhiteTextOnTheDarkPrimaryKeepsAtLeastTheTextContrast(string primary)
    {
        BrandColour darkPrimary = Colour(Generate(primary).Dark["--crm-primary-600"]);

        Assert.True(BrandColour.White.ContrastWith(darkPrimary) >= BrandPalette.TextContrast - 0.1);
    }

    [Theory]
    [MemberData(nameof(DarkPrimaries))]
    public void TheDarkAccentIsAtLeastAsLightAsTheMinimum(string accent)
    {
        double luminance = Colour(Generate("#0E6655", accent).Dark["--crm-accent-600"]).RelativeLuminance;

        Assert.True(luminance >= BrandTokenGenerator.DarkMinLuminance - Epsilon);
    }

    [Fact]
    public void AColourAlreadyInsideTheWindowIsReturnedUnchanged()
    {
        // #6F6F6F has a relative luminance of about 0.159, inside [0.115, 0.180].
        BrandTokensDto tokens = Generate("#6F6F6F", "#6F6F6F");

        Assert.Equal("#6F6F6F", tokens.Dark["--crm-primary-600"]);
        Assert.Equal("#6F6F6F", tokens.Dark["--crm-accent-600"]);
    }

    [Fact]
    public void ALightPrimaryIsDarkenedForTheDarkTheme()
    {
        double light = Colour("#FFD400").RelativeLuminance;
        double dark = Colour(Generate("#FFD400").Dark["--crm-primary-600"]).RelativeLuminance;

        Assert.True(dark < light);
    }

    [Fact]
    public void ADarkPrimaryIsLightenedForTheDarkTheme()
    {
        double light = Colour("#0B1F4B").RelativeLuminance;
        double dark = Colour(Generate("#0B1F4B").Dark["--crm-primary-600"]).RelativeLuminance;

        Assert.True(dark > light);
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#010101")]
    [InlineData("#FEFEFE")]
    [InlineData("#FF00FF")]
    public void ExtremeInputsNeverThrow(string hex)
    {
        BrandTokensDto tokens = Generate(hex, hex);

        Assert.Equal(22, tokens.Dark.Count);
        Assert.Equal(22, tokens.Light.Count);
    }

    [Fact]
    public void AGreyWithZeroChromaProducesAGreyScale()
    {
        BrandTokensDto tokens = Generate("#808080", "#808080");

        Assert.All(Steps, step =>
        {
            BrandColour colour = Colour(tokens.Light[$"--crm-primary-{step}"]);
            Assert.InRange(Math.Abs(colour.R - colour.G), 0, 2);
            Assert.InRange(Math.Abs(colour.G - colour.B), 0, 2);
        });
    }

    [Fact]
    public void GenerateRejectsNullArguments()
    {
        Func<BrandColour, BrandColour, BrandTokensDto> generate = BrandTokenGenerator.Generate;

        NullArguments.AssertThrowsArgumentNull(generate, null, Colours.Accent);
        NullArguments.AssertThrowsArgumentNull(generate, Colours.Primary, null);
    }

    [Fact]
    public void ThePrimaryAndAccentScalesAreIndependent()
    {
        BrandTokensDto tokens = Generate("#0E6655", "#B7791F");

        Assert.NotEqual(tokens.Light["--crm-primary-300"], tokens.Light["--crm-accent-300"]);
    }
}
