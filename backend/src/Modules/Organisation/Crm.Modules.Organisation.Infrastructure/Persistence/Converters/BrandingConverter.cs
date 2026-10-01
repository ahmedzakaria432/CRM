using System.Text.Json;
using Crm.Modules.Organisation.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crm.Modules.Organisation.Infrastructure.Persistence.Converters;

/// <summary>Branding to and from JSON (Story 58, D1). A ValueConverter for a value object is the CLAUDE.md-sanctioned
/// mapping. EF never passes null to a converter, so null stays SQL NULL (the product defaults).</summary>
public sealed class BrandingConverter() : ValueConverter<Branding?, string>(b => Serialize(b), s => Deserialize(s))
{
    private sealed record Row(string NameAr, string NameEn, string Primary, string Accent, string ArabicFont, Guid? LightLogo, Guid? DarkLogo);

    private static string Serialize(Branding? b)
    {
        ArgumentNullException.ThrowIfNull(b);

        return JsonSerializer.Serialize(new Row(
            b.DisplayName.Arabic, b.DisplayName.English, b.Palette.Primary.Hex, b.Palette.Accent.Hex,
            b.ArabicFont.ToString(), b.LightLogo?.Value, b.DarkLogo?.Value));
    }

    private static Branding Deserialize(string json)
    {
        Row row = JsonSerializer.Deserialize<Row>(json) ?? throw new InvalidOperationException("Stored branding is empty.");
        return Branding.Create(
            LocalizedText.Create(row.NameAr, row.NameEn),
            BrandPalette.Create(BrandColour.Create(row.Primary), BrandColour.Create(row.Accent)),
            Enum.Parse<ArabicFont>(row.ArabicFont),
            row.LightLogo is { } l ? new BrandLogoId(l) : null,
            row.DarkLogo is { } d ? new BrandLogoId(d) : null);
    }
}
