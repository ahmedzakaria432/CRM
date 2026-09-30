namespace Crm.Modules.Identity.Contracts.DevSeed;

/// <summary>Well-known Development sign-in emails (the seeded users share the ids in Organisation's
/// OrganisationDevSeed). The password is Identity:DevSeed:Password in appsettings.Development.json. Never used
/// outside Development. No module API in Story 07 - no other module needs one yet.</summary>
public static class IdentityDevSeed
{
    public const string AdminEmail = "admin@crm.local";
    public const string SupervisorEmail = "supervisor@crm.local";
    public const string AgentAEmail = "agent.a@crm.local";
    public const string AgentBEmail = "agent.b@crm.local";
}
