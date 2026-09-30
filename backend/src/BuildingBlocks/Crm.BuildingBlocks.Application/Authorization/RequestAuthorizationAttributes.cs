namespace Crm.BuildingBlocks.Application.Authorization;

/// <summary>The caller needs this permission in the active scope (CrmPermissions).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequirePermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}

/// <summary>Any signed-in staff user; no permission.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequireAuthenticatedUserAttribute : Attribute;

/// <summary>No user needed: sign-in, refresh, sign-out, and in-process sign-in lookups. Never on a request an HTTP
/// endpoint exposes for data that belongs to someone.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AllowAnonymousRequestAttribute : Attribute;
