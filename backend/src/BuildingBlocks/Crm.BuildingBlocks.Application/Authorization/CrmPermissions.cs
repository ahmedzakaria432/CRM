using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.BuildingBlocks.Application.Authorization;

/// <summary>The code-defined permission catalogue and the fixed role -> permission map (Story 07). Lives in the
/// building blocks, like CrmRoles, because AuthorizationBehavior needs it and a building block cannot reference a
/// module. Adding a permission is a code change here plus the requests that declare it.</summary>
public static class CrmPermissions
{
    public const string TicketsRead = "tickets.read";
    public const string TicketsWrite = "tickets.write";
    public const string TicketsAssign = "tickets.assign";
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";
    public const string AdminOrganisation = "admin.organisation";
    public const string AdminUsers = "admin.users";
    public const string AdminBranding = "admin.branding";

    public static readonly IReadOnlyList<string> All =
        [TicketsRead, TicketsWrite, TicketsAssign, CustomersRead, CustomersWrite, AdminOrganisation, AdminUsers, AdminBranding];

    private static readonly IReadOnlyList<string> AgentPermissions = [TicketsRead, TicketsWrite, CustomersRead, CustomersWrite];

    private static readonly Dictionary<string, IReadOnlyList<string>> ByRole = new(StringComparer.Ordinal)
    {
        [CrmRoles.Agent] = AgentPermissions,
        [CrmRoles.Supervisor] = [.. AgentPermissions, TicketsAssign],
        [CrmRoles.Administrator] = All,
    };

    /// <summary>Union of the roles' permissions, ordinal-sorted. An unknown role grants nothing.</summary>
    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles) =>
        [.. roles.SelectMany(r => ByRole.TryGetValue(r, out IReadOnlyList<string>? p) ? p : [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public static bool IsKnown(string permission) => All.Contains(permission, StringComparer.Ordinal);
}
