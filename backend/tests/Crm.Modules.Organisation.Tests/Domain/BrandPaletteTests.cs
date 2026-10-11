using System.Globalization;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class BrandPaletteTests
{
    private static BrandColour Colour(string hex) => BrandColour.Create(hex);

    [Fact]
    public void ConstantsAreTheWcagAaThresholds()
    {
        Assert.Equal(4.5, BrandPalette.TextContrast);
        Assert.Equal(3.0, BrandPalette.NonTextContrast);
    }

    [Fact]
    public void EvaluateReturnsThePrimaryCheckThenTheAccentCheck()
    {
        IReadOnlyList<ContrastCheck> checks = BrandPalette.Evaluate(Colours.Primary, Colours.Accent);

        Assert.Equal(2, checks.Count);
        Assert.Equal(ContrastCheck.Names.PrimaryText, checks[0].Name);
        Assert.Equal(4.5, checks[0].Required);
        Assert.Equal(BrandColour.White.ContrastWith(Colours.Primary), checks[0].Ratio, 10);
        Assert.Equal(ContrastCheck.Names.AccentOnSurface, checks[1].Name);
        Assert.Equal(3.0, checks[1].Required);
        Assert.Equal(BrandColour.White.ContrastWith(Colours.Accent), checks[1].Ratio, 10);
    }

    [Fact]
    public void EvaluateRejectsNullArguments()
    {
        Func<BrandColour, BrandColour, IReadOnlyList<ContrastCheck>> evaluate = BrandPalette.Evaluate;

        NullArguments.AssertThrowsArgumentNull(evaluate, null, Colours.Accent);
        NullArguments.AssertThrowsArgumentNull(evaluate, Colours.Primary, null);
    }

    [Theory]
    [InlineData("#0E6655", "#B7791F")]
    [InlineData("#000000", "#000000")]
    [InlineData("#767676", "#767676")]
    public void CreateAcceptsPalettesThatPassBothChecks(string primary, string accent)
    {
        BrandPalette palette = BrandPalette.Create(Colour(primary), Colour(accent));

        Assert.Equal(primary, palette.Primary.Hex);
        Assert.Equal(accent, palette.Accent.Hex);
    }

    [Fact]
    public void CreateRejectsAPrimaryThatFailsTheTextContrast()
    {
        var exception = Assert.Throws<DomainException>(() => BrandPalette.Create(Colour("#FFD400"), Colours.Accent));

        Assert.Equal("Organisation.BrandingContrastTooLow", exception.Error.Code);
        Assert.Contains("PrimaryText", exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRejectsGreySeventySevenWithTheFiguresInTheMessage()
    {
        var exception = Assert.Throws<DomainException>(() => BrandPalette.Create(Colour("#777777"), Colours.Accent));

        Assert.Contains("4.48:1", exception.Error.Message, StringComparison.Ordinal);
        Assert.Contains("4.5:1", exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMessageUsesInvariantDigitsUnderAnArabicCulture()
    {
        using UiCulture arabic = UiCulture.Use("ar-SA");

        var exception = Assert.Throws<DomainException>(() => BrandPalette.Create(Colour("#777777"), Colours.Accent));

        Assert.Equal("PrimaryText contrast is 4.48:1; WCAG AA needs 4.5:1.", exception.Error.Message);
        Assert.Equal("ar-SA", CultureInfo.CurrentCulture.Name);
    }

    [Fact]
    public void CreateRejectsAnAccentThatFailsTheNonTextContrast()
    {
        var exception = Assert.Throws<DomainException>(() => BrandPalette.Create(Colours.Primary, Colour("#F5F5F5")));

        Assert.Equal("Organisation.BrandingContrastTooLow", exception.Error.Code);
        Assert.Contains("AccentOnSurface", exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePrimaryFailureIsReportedBeforeTheAccentFailure()
    {
        var exception = Assert.Throws<DomainException>(() => BrandPalette.Create(Colour("#FFFFFF"), Colour("#FFFFFF")));

        Assert.Contains("PrimaryText", exception.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("AccentOnSurface", exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRejectsNullArguments()
    {
        Func<BrandColour, BrandColour, BrandPalette> create = BrandPalette.Create;

        NullArguments.AssertThrowsArgumentNull(create, null, Colours.Accent);
        NullArguments.AssertThrowsArgumentNull(create, Colours.Primary, null);
    }

    [Fact]
    public void PalettesAreEqualWhenPrimaryAndAccentMatch()
    {
        BrandPalette first = BrandPalette.Create(Colours.Primary, Colours.Accent);
        BrandPalette second = BrandPalette.Create(Colour("#0e6655"), Colour("#b7791f"));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void PalettesDifferWhenEitherColourDiffers()
    {
        BrandPalette original = BrandPalette.Create(Colours.Primary, Colours.Accent);

        Assert.NotEqual(original, BrandPalette.Create(Colour("#000000"), Colours.Accent));
        Assert.NotEqual(original, BrandPalette.Create(Colours.Primary, Colour("#000000")));
    }
}
