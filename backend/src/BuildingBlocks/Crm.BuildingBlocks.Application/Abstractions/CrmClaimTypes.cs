namespace Crm.BuildingBlocks.Application.Abstractions;

/// <summary>
/// The claim names Story 07's JWT must carry: sub, tenant_id, one membership per membership, one role per
/// distinct role (Story 06). Logging, auditing, scoping, and cache keys read these - do not rename one without
/// updating every reader.
/// </summary>
public static class CrmClaimTypes
{
    public const string UserId = "sub";
    public const string TenantId = "tenant_id";

    /// <summary>One per membership: "&lt;departmentId&gt;|&lt;branchId&gt;|&lt;role&gt;" (Guid "D" format). Story 06.</summary>
    public const string Membership = "membership";
    public const string Role = "role";
}
