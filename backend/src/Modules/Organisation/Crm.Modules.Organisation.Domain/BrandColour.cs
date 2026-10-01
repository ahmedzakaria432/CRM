using System.Globalization;
using System.Text.RegularExpressions;
using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Domain;

/// <summary>An sRGB colour as #RRGGBB (upper-case). WCAG 2.x luminance/contrast live here because the contrast
/// rule is a domain invariant (Story 58, D6).</summary>
public sealed partial class BrandColour : ValueObject
{
    public static readonly BrandColour White = Create("#FFFFFF");

    public string Hex { get; }

    public byte R { get; }

    public byte G { get; }

    public byte B { get; }

    private BrandColour(string hex, byte r, byte g, byte b)
    {
        Hex = hex;
        R = r;
        G = g;
        B = b;
    }

    public static BrandColour Create(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        string trimmed = hex.Trim();
        if (!HexPattern().IsMatch(trimmed))
        {
            throw new DomainException(new DomainError("Organisation.BrandColourInvalid", "A colour must be a hex value like #1A2B3C."));
        }

        byte Channel(int start) => byte.Parse(trimmed.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return new BrandColour(trimmed.ToUpperInvariant(), Channel(1), Channel(3), Channel(5));
    }

    /// <summary>Linear-light channels in [0, 1] clamp and round to the nearest sRGB byte.</summary>
    public static BrandColour FromLinearRgb(double r, double g, double b)
    {
        static byte Encode(double linear)
        {
            double c = Math.Clamp(linear, 0, 1);
            double encoded = c <= 0.0031308 ? 12.92 * c : (1.055 * Math.Pow(c, 1 / 2.4)) - 0.055;
            return (byte)Math.Round(Math.Clamp(encoded, 0, 1) * 255, MidpointRounding.AwayFromZero);
        }

        byte red = Encode(r);
        byte green = Encode(g);
        byte blue = Encode(b);
        return new BrandColour(string.Create(CultureInfo.InvariantCulture, $"#{red:X2}{green:X2}{blue:X2}"), red, green, blue);
    }

    public (double R, double G, double B) ToLinearRgb() => (Linear(R), Linear(G), Linear(B));

    /// <summary>WCAG 2.x relative luminance.</summary>
    public double RelativeLuminance
    {
        get
        {
            (double r, double g, double b) = ToLinearRgb();
            return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        }
    }

    public double ContrastWith(BrandColour other)
    {
        ArgumentNullException.ThrowIfNull(other);

        double a = RelativeLuminance;
        double b = other.RelativeLuminance;
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Linear(byte channel)
    {
        double c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Hex;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexPattern();
}
