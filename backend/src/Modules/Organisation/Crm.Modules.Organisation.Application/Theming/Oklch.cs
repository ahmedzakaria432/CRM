using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Application.Theming;

/// <summary>OKLCH colour (H in radians) using Bjorn Ottosson's OKLab matrices; used to derive the 11-step scales (Story 58, D7).</summary>
internal readonly record struct Oklch(double L, double C, double H)
{
    public static Oklch FromColour(BrandColour colour)
    {
        ArgumentNullException.ThrowIfNull(colour);

        (double r, double g, double b) = colour.ToLinearRgb();
        double l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        double m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        double s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));
        double okL = (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s);
        double a = (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s);
        double bb = (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s);
        return new Oklch(okL, Math.Sqrt((a * a) + (bb * bb)), Math.Atan2(bb, a));
    }

    public (double R, double G, double B) ToLinearRgb()
    {
        double a = C * Math.Cos(H);
        double b = C * Math.Sin(H);
        double l = Math.Pow(L + (0.3963377774 * a) + (0.2158037573 * b), 3);
        double m = Math.Pow(L - (0.1055613458 * a) - (0.0638541728 * b), 3);
        double s = Math.Pow(L - (0.0894841775 * a) - (1.2914855480 * b), 3);
        return ((4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s),
                (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s),
                (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s));
    }

    public bool InGamut()
    {
        (double r, double g, double b) = ToLinearRgb();
        const double e = 1e-6;
        return r >= -e && r <= 1 + e && g >= -e && g <= 1 + e && b >= -e && b <= 1 + e;
    }

    /// <summary>Keeps L and H; bisects chroma (24 steps) down to the largest in-gamut value.</summary>
    public Oklch ClampChroma()
    {
        if (InGamut())
        {
            return this;
        }

        double lo = 0;
        double hi = C;
        for (int i = 0; i < 24; i++)
        {
            double mid = (lo + hi) / 2;
            if ((this with { C = mid }).InGamut())
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return this with { C = lo };
    }

    public BrandColour ToColour()
    {
        (double r, double g, double b) = ClampChroma().ToLinearRgb();
        return BrandColour.FromLinearRgb(r, g, b);
    }
}
