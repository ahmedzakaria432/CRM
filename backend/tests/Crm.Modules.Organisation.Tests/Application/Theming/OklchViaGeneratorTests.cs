using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Theming;
using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Tests.Application.Theming;

/// <summary>Oklch is internal and the module has no InternalsVisibleTo (adding one is a production change), so the OKLCH
/// conversions are exercised through BrandTokenGenerator, their only caller.</summary>
public sealed class OklchViaGeneratorTests
{
    private static BrandTokensDto Generate(string hex) => BrandTokenGenerator.Generate(BrandColour.Create(hex), BrandColour.Create(hex));

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("#00FF00")]
    [InlineData("#0000FF")]
    [InlineData("#FF00FF")]
    [InlineData("#00FFFF")]
    [InlineData("#FFD400")]
    public void SaturatedColoursNeedGamutClampingAndEveryStepStaysAValidHex(string hex)
    {
        BrandTokensDto tokens = Generate(hex);

        Assert.All(tokens.Light.Values, value => Assert.Matches("^#[0-9A-F]{6}$", value));
        Assert.All(tokens.Dark.Values, value => Assert.Matches("^#[0-9A-F]{6}$", value));
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void BlackAndWhiteAnchorsProduceValidScales(string hex)
    {
        BrandTokensDto tokens = Generate(hex);

        Assert.Equal(22, tokens.Light.Count);
        Assert.Equal(hex, tokens.Light["--crm-primary-600"]);
    }

    [Fact]
    public void WhiteAnchorsKeepEveryLightStepWhite()
    {
        // Lightness is already at the ceiling and chroma is zero, so the light ladder collapses to white above step 600.
        BrandTokensDto tokens = Generate("#FFFFFF");

        Assert.Equal("#FFFFFF", tokens.Light["--crm-primary-50"]);
        Assert.Equal("#FFFFFF", tokens.Light["--crm-primary-500"]);
    }

    [Theory]
    [InlineData("#0E6655")]
    [InlineData("#B7791F")]
    [InlineData("#145EC1")]
    [InlineData("#FF0000")]
    [InlineData("#123456")]
    [InlineData("#ABCDEF")]
    public void ColourToOklchAndBackIsExactAtStepSixHundred(string hex)
    {
        Assert.Equal(hex, Generate(hex).Light["--crm-primary-600"]);
    }

    [Fact]
    public void ADarkAnchorKeepsItsHueWhenLightened()
    {
        // #0B1F4B is a dark blue: the dark-theme primary is lighter but must stay blue-dominant.
        BrandColour darkPrimary = BrandColour.Create(Generate("#0B1F4B").Dark["--crm-primary-600"]);

        Assert.True(darkPrimary.B > darkPrimary.R);
        Assert.True(darkPrimary.B > darkPrimary.G);
    }
}
