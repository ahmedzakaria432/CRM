namespace Crm.Modules.Organisation.Application;

/// <summary>Cache keys and tags: {module}:{aggregate-plural}:{id} (Story 58, D12).</summary>
public static class OrganisationCacheKeys
{
    /// <summary>One tag for every tenant's branding - a branding command cannot name the organisation code.</summary>
    public const string Brandings = "organisation:brandings";

    public static string PublicBranding(string organisationCode)
    {
        ArgumentNullException.ThrowIfNull(organisationCode);

        return $"organisation:brandings:code:{organisationCode.Trim().ToUpperInvariant()}";
    }

    /// <summary>CachingBehavior prefixes the caller's tenant, so the current tenant needs no id in the key.</summary>
    public const string CurrentBranding = "organisation:brandings:current";
}
