using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class BrandingTests
{
    private static readonly BrandLogoId Light = BrandLogoId.New();
    private static readonly BrandLogoId Dark = BrandLogoId.New();

    [Fact]
    public void CreateWithoutLogos()
    {
        Branding branding = Brandings.Valid();

        Assert.Equal("Brand", branding.DisplayName.English);
        Assert.Equal("#0E6655", branding.Palette.Primary.Hex);
        Assert.Equal(ArabicFont.Tajawal, branding.ArabicFont);
        Assert.Null(branding.LightLogo);
        Assert.Null(branding.DarkLogo);
    }

    [Fact]
    public void CreateWithBothLogos()
    {
        Branding branding = Brandings.Valid(Light, Dark);

        Assert.Equal(Light, branding.LightLogo);
        Assert.Equal(Dark, branding.DarkLogo);
    }

    [Fact]
    public void ALightLogoWithoutADarkLogoIsAllowed()
    {
        Branding branding = Brandings.Valid(Light);

        Assert.Equal(Light, branding.LightLogo);
        Assert.Null(branding.DarkLogo);
    }

    [Fact]
    public void ADarkLogoWithoutALightLogoIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Brandings.Valid(null, Dark));

        Assert.Equal("Organisation.DarkLogoWithoutLight", exception.Error.Code);
    }

    [Fact]
    public void AnUnsupportedFontIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Brandings.Valid(font: (ArabicFont)99));

        Assert.Equal("Organisation.ArabicFontUnsupported", exception.Error.Code);
    }

    [Theory]
    [InlineData(ArabicFont.IbmPlexSansArabic)]
    [InlineData(ArabicFont.NotoKufiArabic)]
    [InlineData(ArabicFont.Tajawal)]
    public void EverySupportedFontIsAccepted(ArabicFont font)
    {
        Assert.Equal(font, Brandings.Valid(font: font).ArabicFont);
    }

    [Fact]
    public void CreateRejectsNullArguments()
    {
        Func<LocalizedText, BrandPalette, ArabicFont, BrandLogoId?, BrandLogoId?, Branding> create = Branding.Create;
        BrandPalette palette = BrandPalette.Create(Colours.Primary, Colours.Accent);

        NullArguments.AssertThrowsArgumentNull(create, null, palette, ArabicFont.Tajawal, null, null);
        NullArguments.AssertThrowsArgumentNull(create, Names.Text(), null, ArabicFont.Tajawal, null, null);
    }

    [Fact]
    public void BrandingsWithTheSameValuesAreEqual()
    {
        Assert.Equal(Brandings.Valid(Light, Dark), Brandings.Valid(Light, Dark));
        Assert.Equal(Brandings.Valid(Light, Dark).GetHashCode(), Brandings.Valid(Light, Dark).GetHashCode());
    }

    [Fact]
    public void ChangingAnySingleComponentMakesBrandingsDifferent()
    {
        Branding original = Brandings.Valid(Light, Dark);

        Assert.NotEqual(original, Brandings.Valid(Light, Dark, english: "Other"));
        Assert.NotEqual(original, Brandings.Valid(Light, Dark, font: ArabicFont.NotoKufiArabic));
        Assert.NotEqual(original, Brandings.WithPrimary("#000000"));
        Assert.NotEqual(original, Brandings.Valid(BrandLogoId.New(), Dark));
        Assert.NotEqual(original, Brandings.Valid(Light, BrandLogoId.New()));
        Assert.NotEqual(original, Brandings.Valid(Light, null));
    }
}
