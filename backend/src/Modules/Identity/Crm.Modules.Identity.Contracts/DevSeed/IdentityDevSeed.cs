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

    /// <summary>Portal account of CustomersDevSeed.AhmedContactId (primary contact, so account admin); the password is
    /// Identity:DevSeed:Password. Story 47.</summary>
    public static readonly Guid AhmedPortalAccountId = Guid.Parse("47000000-0000-0000-0000-000000000001");
}
