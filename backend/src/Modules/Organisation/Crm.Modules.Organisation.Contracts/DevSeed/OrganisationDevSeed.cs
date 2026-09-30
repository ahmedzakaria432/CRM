namespace Crm.Modules.Organisation.Contracts.DevSeed;

/// <summary>Well-known Development ids, as primitives - shared with Story 07's user seed and the Sample seed
/// (Sample is the first organisation-scoped aggregate; Story 11 copies its shape onto Customer). Never used
/// outside Development.</summary>
public static class OrganisationDevSeed
{
    public static readonly Guid TenantId = Guid.Parse("06000000-0000-0000-0000-000000000001");

    public static readonly Guid RiyadhBranchId = Guid.Parse("06000000-0000-0000-0001-000000000001");
    public static readonly Guid JeddahBranchId = Guid.Parse("06000000-0000-0000-0001-000000000002");

    public static readonly Guid RiyadhCareDepartmentId = Guid.Parse("06000000-0000-0000-0002-000000000001");
    public static readonly Guid RiyadhBillingDepartmentId = Guid.Parse("06000000-0000-0000-0002-000000000002");
    public static readonly Guid JeddahCareDepartmentId = Guid.Parse("06000000-0000-0000-0002-000000000003");

    /// <summary>Administrator in all three departments.</summary>
    public static readonly Guid AdminUserId = Guid.Parse("06000000-0000-0000-0003-000000000001");

    /// <summary>Supervisor in RYD-CARE, Agent in RYD-BILL (two departments, two roles).</summary>
    public static readonly Guid SupervisorUserId = Guid.Parse("06000000-0000-0000-0003-000000000002");

    /// <summary>Agent in RYD-CARE.</summary>
    public static readonly Guid AgentAUserId = Guid.Parse("06000000-0000-0000-0003-000000000003");

    /// <summary>Agent in JED-CARE (other department AND other branch).</summary>
    public static readonly Guid AgentBUserId = Guid.Parse("06000000-0000-0000-0003-000000000004");

    public static readonly Guid AdminCareMembershipId = Guid.Parse("06000000-0000-0000-0004-000000000001");
    public static readonly Guid AdminBillingMembershipId = Guid.Parse("06000000-0000-0000-0004-000000000002");
    public static readonly Guid AdminJeddahCareMembershipId = Guid.Parse("06000000-0000-0000-0004-000000000003");
    public static readonly Guid SupervisorCareMembershipId = Guid.Parse("06000000-0000-0000-0004-000000000004");
    public static readonly Guid SupervisorBillingMembershipId = Guid.Parse("06000000-0000-0000-0004-000000000005");
    public static readonly Guid AgentACareMembershipId = Guid.Parse("06000000-0000-0000-0004-000000000006");
    public static readonly Guid AgentBJeddahCareMembershipId = Guid.Parse("06000000-0000-0000-0004-000000000007");
}
