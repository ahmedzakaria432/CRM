using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Domain;

/// <summary>An organisation's look: display name, palette, Arabic face and logos (Story 58, D1).</summary>
public sealed class Branding : ValueObject
{
    public LocalizedText DisplayName { get; }

    public BrandPalette Palette { get; }

    public ArabicFont ArabicFont { get; }

    public BrandLogoId? LightLogo { get; }

    public BrandLogoId? DarkLogo { get; }

    private Branding(LocalizedText displayName, BrandPalette palette, ArabicFont arabicFont, BrandLogoId? lightLogo, BrandLogoId? darkLogo)
    {
        DisplayName = displayName;
        Palette = palette;
        ArabicFont = arabicFont;
        LightLogo = lightLogo;
        DarkLogo = darkLogo;
    }

    public static Branding Create(LocalizedText displayName, BrandPalette palette, ArabicFont arabicFont, BrandLogoId? lightLogo, BrandLogoId? darkLogo)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(palette);

        if (!Enum.IsDefined(arabicFont))
        {
            throw new DomainException(new DomainError("Organisation.ArabicFontUnsupported", "The Arabic font is not one of the supported fonts."));
        }

        if (darkLogo is not null && lightLogo is null)
        {
            throw new DomainException(new DomainError("Organisation.DarkLogoWithoutLight", "A dark-theme logo needs a light-theme logo."));
        }

        return new Branding(displayName, palette, arabicFont, lightLogo, darkLogo);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DisplayName;
        yield return Palette;
        yield return ArabicFont;
        yield return LightLogo;
        yield return DarkLogo;
    }
}
