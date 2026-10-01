using System.Globalization;
using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Domain;

/// <summary>The primary and accent colours of a branding, valid only when they pass WCAG AA (Story 58, D6).</summary>
public sealed class BrandPalette : ValueObject
{
    public const double TextContrast = 4.5;
    public const double NonTextContrast = 3.0;

    public BrandColour Primary { get; }

    public BrandColour Accent { get; }

    private BrandPalette(BrandColour primary, BrandColour accent)
    {
        Primary = primary;
        Accent = accent;
    }

    public static IReadOnlyList<ContrastCheck> Evaluate(BrandColour primary, BrandColour accent)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(accent);

        return
        [
            new(ContrastCheck.Names.PrimaryText, BrandColour.White.ContrastWith(primary), TextContrast),
            new(ContrastCheck.Names.AccentOnSurface, BrandColour.White.ContrastWith(accent), NonTextContrast),
        ];
    }

    public static BrandPalette Create(BrandColour primary, BrandColour accent)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(accent);

        ContrastCheck? failing = Evaluate(primary, accent).FirstOrDefault(c => !c.Passes);
        if (failing is not null)
        {
            throw new DomainException(new DomainError("Organisation.BrandingContrastTooLow", string.Create(CultureInfo.InvariantCulture,
                $"{failing.Name} contrast is {failing.Ratio:0.00}:1; WCAG AA needs {failing.Required:0.0}:1.")));
        }

        return new BrandPalette(primary, accent);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Primary;
        yield return Accent;
    }
}
