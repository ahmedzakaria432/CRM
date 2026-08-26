namespace Crm.Api.Areas;

/// <summary>
/// One Area per persona. All drives both the Swagger document registration and the UI picker
/// (Program.cs), so adding an Area is a one-line change here.
/// </summary>
public static class CrmAreas
{
    public const string Agent = "Agent";
    public const string Portal = "Portal";
    public const string Admin = "Admin";
    public const string Public = "Public";

    public static readonly IReadOnlyList<string> All = [Agent, Portal, Admin, Public];
}
