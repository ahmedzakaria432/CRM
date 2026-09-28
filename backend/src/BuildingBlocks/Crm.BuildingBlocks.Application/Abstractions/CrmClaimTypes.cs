namespace Crm.BuildingBlocks.Application.Abstractions;

/// <summary>
/// The claim names Story 07's JWT must carry. Logging, auditing, scoping, and cache keys read these - do not
/// rename one without updating every reader.
/// </summary>
public static class CrmClaimTypes
{
    public const string UserId = "sub";
    public const string TenantId = "tenant_id";
    public const string DepartmentId = "department_id";
    public const string BranchId = "branch_id";
}
