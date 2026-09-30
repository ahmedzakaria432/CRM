namespace Crm.BuildingBlocks.Application.Abstractions;

/// <summary>
/// The claims Story 07's staff JWT carries: sub, name, tenant_id, sid, one membership per membership, one role per
/// distinct role. Logging, auditing, scoping, and cache keys read these - do not rename one without
/// updating every reader.
/// </summary>
public static class CrmClaimTypes
{
    public const string UserId = "sub";
    public const string DisplayName = "name";
    public const string TenantId = "tenant_id";

    /// <summary>The refresh-token family (session) id. Story 07.</summary>
    public const string SessionId = "sid";

    /// <summary>One per membership: "&lt;departmentId&gt;|&lt;branchId&gt;|&lt;role&gt;" (Guid "D" format). Story 06.</summary>
    public const string Membership = "membership";
    public const string Role = "role";
}
