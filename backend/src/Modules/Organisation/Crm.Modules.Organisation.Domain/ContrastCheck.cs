namespace Crm.Modules.Organisation.Domain;

/// <summary>One WCAG AA check of a palette (D6). Name is one of ContrastCheck.Names.</summary>
public sealed record ContrastCheck(string Name, double Ratio, double Required)
{
    public bool Passes => Ratio >= Required;

    public static class Names
    {
        public const string PrimaryText = "PrimaryText";
        public const string AccentOnSurface = "AccentOnSurface";
    }
}
