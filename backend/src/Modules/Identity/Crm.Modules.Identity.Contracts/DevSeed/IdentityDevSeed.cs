namespace Crm.Modules.Identity.Contracts.DevSeed;

/// <summary>Well-known Development sign-in emails (the seeded users share the ids in Organisation's
/// OrganisationDevSeed). The password is Identity:DevSeed:Password in appsettings.Development.json. Never used
/// outside Development. Story 15 added IIdentityModuleApi (staff display names).</summary>
public static class IdentityDevSeed
{
    public const string AdminEmail = "admin@crm.local";
    public const string SupervisorEmail = "supervisor@crm.local";
    public const string AgentAEmail = "agent.a@crm.local";
    public const string AgentBEmail = "agent.b@crm.local";

    /// <summary>Agent in RYD-BILL with no tickets assigned (Story 59).</summary>
    public const string AgentCEmail = "agent.c@crm.local";

    /// <summary>Supervisor in RYD-CARE and JED-CARE (Story 59).</summary>
    public const string SupervisorBEmail = "supervisor.b@crm.local";

    /// <summary>Deactivated agent - sign-in is refused (Story 59).</summary>
    public const string InactiveAgentEmail = "agent.inactive@crm.local";

    /// <summary>Portal account of CustomersDevSeed.AhmedContactId (primary contact, so account admin); the password is
    /// Identity:DevSeed:Password. Story 47.</summary>
    public static readonly Guid AhmedPortalAccountId = Guid.Parse("47000000-0000-0000-0000-000000000001");

    /// <summary>Al-Noor Clinics primary contact - portal account admin (Story 59).</summary>
    public static readonly Guid MonaPortalAccountId = Guid.Parse("47000000-0000-0000-0000-000000000002");

    /// <summary>Al-Noor Clinics non-primary contact - sees only his own requests (Story 59).</summary>
    public static readonly Guid YousefPortalAccountId = Guid.Parse("47000000-0000-0000-0000-000000000003");

    /// <summary>Al-Noor Clinics contact whose portal access is off - sign-in is refused (Story 59).</summary>
    public static readonly Guid HudaPortalAccountId = Guid.Parse("47000000-0000-0000-0000-000000000004");

    /// <summary>Individual customer with no requests - the portal empty state (Story 59).</summary>
    public static readonly Guid FaisalPortalAccountId = Guid.Parse("47000000-0000-0000-0000-000000000005");
}
