namespace Crm.BuildingBlocks.Application.Localization;

/// <summary>The two supported cultures. Neutral on purpose: "ar-SA" would default .NET to the Um Al-Qura
/// (Hijri) calendar, which is out of scope. A request for ar-SA falls back to "ar" (Probe 3).</summary>
public static class CrmCultures
{
    public const string English = "en";
    public const string Arabic = "ar";
    public const string Default = English;
    public static readonly IReadOnlyList<string> All = [English, Arabic];
}
