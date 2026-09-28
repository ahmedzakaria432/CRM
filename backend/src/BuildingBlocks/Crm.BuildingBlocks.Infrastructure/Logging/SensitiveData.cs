namespace Crm.BuildingBlocks.Infrastructure.Logging;

/// <summary>The one rule for which log property names are secrets. Matched on the normalised name, so
/// "api_key", "ApiKey", and "X-Api-Key" all match.</summary>
internal static class SensitiveData
{
    public const string Redacted = "***";

    private static readonly string[] Fragments =
        ["password", "passwd", "secret", "token", "apikey", "authorization", "connectionstring", "credential"];

    public static bool IsSensitive(string name)
    {
        string normalised = new(name.Where(c => c is not ('_' or '-' or '.')).Select(char.ToLowerInvariant).ToArray());
        return Fragments.Any(fragment => normalised.Contains(fragment, StringComparison.Ordinal));
    }
}
