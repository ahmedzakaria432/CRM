namespace Crm.BuildingBlocks.Application.Abstractions;

public static class CrmAuthenticationSchemes
{
    /// <summary>Staff JWT bearer (Agent + Admin Areas).</summary>
    public const string Staff = "Staff";

    /// <summary>Portal customer JWT bearer (Portal Area). Story 47.</summary>
    public const string Portal = "Portal";
}
