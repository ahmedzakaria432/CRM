using System.Globalization;
using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Application.Theming;

/// <summary>Defaults shown in the admin form when nothing is saved (Story 58, D10).</summary>
public static class BrandDefaults
{
    public const string Primary = "#145EC1";
    public const string Accent = "#0F766E";
}

/// <summary>Derives the 11-step primary and accent scales for light and dark (Story 58, D7). Step 600 of the light
/// scale is exactly the chosen colour; the dark anchor is moved in OKLCH lightness into a luminance window so white
/// text on it and the dark surface against it both stay readable. The domain validates only the light palette:
/// the dark derivation never fails.</summary>
public static class BrandTokenGenerator
{
    // tokens.css primary ladder: lightness and chroma / 0.17 per step.
    private static readonly (string Step, double L, double Chroma)[] Ladder =
    [
        ("50", 0.97, 0.12), ("100", 0.93, 0.26), ("200", 0.87, 0.47), ("300", 0.79, 0.65), ("400", 0.70, 0.82),
        ("500", 0.60, 1.0), ("600", 0.50, 1.0), ("700", 0.42, 0.88), ("800", 0.35, 0.71), ("900", 0.28, 0.53), ("950", 0.19, 0.35),
    ];

    /// <summary>At least 3:1 against --crm-neutral-950 (D7).</summary>
    public const double DarkMinLuminance = 0.115;

    /// <summary>White text at least 4.5:1 (D7).</summary>
    public const double DarkPrimaryMaxLuminance = 0.180;

    public static BrandTokensDto Generate(BrandColour primary, BrandColour accent)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(accent);

        var light = new Dictionary<string, string>(StringComparer.Ordinal);
        var dark = new Dictionary<string, string>(StringComparer.Ordinal);
        AddScale(light, "primary", primary);
        AddScale(light, "accent", accent);
        AddScale(dark, "primary", ForDark(primary, DarkMinLuminance, DarkPrimaryMaxLuminance));
        AddScale(dark, "accent", ForDark(accent, DarkMinLuminance, 1.0));
        return new BrandTokensDto(light, dark);
    }

    private static void AddScale(Dictionary<string, string> map, string name, BrandColour anchor)
    {
        Oklch a = Oklch.FromColour(anchor);
        foreach ((string step, double ladderL, double chroma) in Ladder)
        {
            string key = string.Create(CultureInfo.InvariantCulture, $"--crm-{name}-{step}");
            if (step == "600")
            {
                map[key] = anchor.Hex;
                continue;
            }

            double l = ladderL > 0.50
                ? (a.L >= 0.97 ? a.L : a.L + ((0.97 - a.L) * (ladderL - 0.50) / 0.47))
                : a.L * ladderL / 0.50;
            map[key] = new Oklch(l, a.C * chroma, a.H).ToColour().Hex;
        }
    }

    private static BrandColour ForDark(BrandColour colour, double min, double max)
    {
        double luminance = colour.RelativeLuminance;
        if (luminance >= min && luminance <= max)
        {
            return colour;
        }

        Oklch a = Oklch.FromColour(colour);
        if (luminance < min)
        {
            // Smallest lightness (searching upward from the colour) whose luminance reaches the minimum.
            double lo = a.L;
            double hi = 1;
            for (int i = 0; i < 24; i++)
            {
                double mid = (lo + hi) / 2;
                if (new Oklch(mid, a.C, a.H).ToColour().RelativeLuminance >= min)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid;
                }
            }

            return new Oklch(hi, a.C, a.H).ToColour();
        }
        else
        {
            // Largest lightness (searching downward) whose luminance stays under the maximum.
            double lo = 0;
            double hi = a.L;
            for (int i = 0; i < 24; i++)
            {
                double mid = (lo + hi) / 2;
                if (new Oklch(mid, a.C, a.H).ToColour().RelativeLuminance <= max)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            return new Oklch(lo, a.C, a.H).ToColour();
        }
    }
}
