namespace Crm.BuildingBlocks.Application.Abstractions;

/// <summary>The string form of Organisation's SystemRole, used by claims and the Administrator policy. Kept
/// in BuildingBlocks (not the Organisation module) because CrmPolicies (Presentation) needs it and a module
/// cannot be referenced by a building block.</summary>
public static class CrmRoles
{
    public const string Administrator = "Administrator";
    public const string Supervisor = "Supervisor";
    public const string Agent = "Agent";
}
