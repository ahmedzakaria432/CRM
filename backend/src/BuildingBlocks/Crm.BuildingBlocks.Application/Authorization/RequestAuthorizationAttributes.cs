namespace Crm.BuildingBlocks.Application.Authorization;

/// <summary>The caller needs this permission in the active scope (CrmPermissions).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequirePermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}

/// <summary>Any signed-in staff user; no permission. A portal principal is refused (403 Authorization.StaffOnly, Story 47).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequireAuthenticatedUserAttribute : Attribute;

/// <summary>No user needed: sign-in, refresh, sign-out, and in-process sign-in lookups (Story 06 memberships, Story 47 portal contacts in Customers, and Story 58 published branding - GetPublicBrandingQuery and GetBrandLogoQuery - which is public by design). Never on a request an HTTP
/// endpoint exposes for data that belongs to someone.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AllowAnonymousRequestAttribute : Attribute;
