using System.Text.Json;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence.Converters;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Infrastructure;

public sealed class BrandingConverterTests
{
    private readonly BrandingConverter _converter = new();

    private string ToJson(Branding branding) => Assert.IsType<string>(_converter.ConvertToProvider(branding));

    private Branding FromJson(string json) => Assert.IsType<Branding>(_converter.ConvertFromProvider(json));

    private static string Json(
        string nameAr = "علامة",
        string nameEn = "Brand",
        string primary = "#0E6655",
        string accent = "#B7791F",
        string font = "Tajawal") =>
        $"{{\"NameAr\":\"{nameAr}\",\"NameEn\":\"{nameEn}\",\"Primary\":\"{primary}\",\"Accent\":\"{accent}\",\"ArabicFont\":\"{font}\",\"LightLogo\":null,\"DarkLogo\":null}}";

    [Fact]
    public void ABrandingWithoutLogosRoundTrips()
    {
        Branding original = Brandings.Valid();

        Assert.Equal(original, FromJson(ToJson(original)));
    }

    [Fact]
    public void ABrandingWithBothLogosRoundTrips()
    {
        Branding original = Brandings.Valid(BrandLogoId.New(), BrandLogoId.New(), font: ArabicFont.NotoKufiArabic);

        Assert.Equal(original, FromJson(ToJson(original)));
    }

    [Fact]
    public void TheJsonCarriesTheExpectedPropertyNamesAndTheFontName()
    {
        var light = BrandLogoId.New();
        string json = ToJson(Brandings.Valid(light, null, "Brand", ArabicFont.Tajawal));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal(
            ["NameAr", "NameEn", "Primary", "Accent", "ArabicFont", "LightLogo", "DarkLogo"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Brand", root.GetProperty("NameEn").GetString());
        Assert.Equal("#0E6655", root.GetProperty("Primary").GetString());
        Assert.Equal("#B7791F", root.GetProperty("Accent").GetString());
        Assert.Equal("Tajawal", root.GetProperty("ArabicFont").GetString());
        Assert.Equal(light.Value, root.GetProperty("LightLogo").GetGuid());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("DarkLogo").ValueKind);
    }

    [Fact]
    public void ArabicTextSurvivesTheRoundTrip()
    {
        Branding original = Branding.Create(
            LocalizedText.Create("مركز الدعم التجريبي", "Demo Support Centre"),
            BrandPalette.Create(Colours.Primary, Colours.Accent),
            ArabicFont.IbmPlexSansArabic,
            null,
            null);

        Branding restored = FromJson(ToJson(original));

        Assert.Equal("مركز الدعم التجريبي", restored.DisplayName.Arabic);
    }

    [Fact]
    public void SerialisingNullThrows()
    {
        Func<Branding?, string> serialise = _converter.ConvertToProviderExpression.Compile();

        Assert.Throws<ArgumentNullException>(() => serialise(null));
    }

    [Fact]
    public void StoredJsonNullThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => _converter.ConvertFromProvider("null"));
    }

    [Fact]
    public void TamperedJsonWhosePaletteFailsContrastThrowsADomainException()
    {
        var exception = Assert.Throws<Crm.SharedKernel.DomainException>(() => _converter.ConvertFromProvider(Json(primary: "#FFFFFF")));

        Assert.Equal("Organisation.BrandingContrastTooLow", exception.Error.Code);
    }

    [Fact]
    public void AnUnknownFontNameThrowsAnArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _converter.ConvertFromProvider(Json(font: "Arial")));
    }

    [Fact]
    public void MalformedJsonThrowsAJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => _converter.ConvertFromProvider("{"));
    }

    [Fact]
    public void ABlankStoredNameThrowsADomainException()
    {
        var exception = Assert.Throws<Crm.SharedKernel.DomainException>(() => _converter.ConvertFromProvider(Json(nameEn: " ")));

        Assert.Equal("Organisation.NameRequired", exception.Error.Code);
    }

    [Fact]
    public void AValidStoredJsonRestoresTheValues()
    {
        Branding restored = FromJson(Json());

        Assert.Equal("Brand", restored.DisplayName.English);
        Assert.Equal("#0E6655", restored.Palette.Primary.Hex);
        Assert.Equal(ArabicFont.Tajawal, restored.ArabicFont);
        Assert.Null(restored.LightLogo);
    }

    [Fact]
    public void ADarkLogoWithoutALightLogoInStoredJsonThrows()
    {
        string json = Json().Replace("\"DarkLogo\":null", $"\"DarkLogo\":\"{Guid.NewGuid()}\"", StringComparison.Ordinal);

        var exception = Assert.Throws<Crm.SharedKernel.DomainException>(() => _converter.ConvertFromProvider(json));

        Assert.Equal("Organisation.DarkLogoWithoutLight", exception.Error.Code);
    }
}
