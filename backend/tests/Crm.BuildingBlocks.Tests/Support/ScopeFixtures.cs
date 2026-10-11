using Crm.SharedKernel.Scoping;

namespace Crm.BuildingBlocks.Tests.Support;

/// <summary>Fixed ids so a failing assertion names the same row every run. Tenant A has two branches and two
/// departments; tenant B is the "someone else" tenant.</summary>
public static class ScopeFixtures
{
    public static readonly Guid TenantAGuid = new("aaaaaaaa-0000-0000-0000-00000000000a");
    public static readonly Guid TenantBGuid = new("bbbbbbbb-0000-0000-0000-00000000000b");
    public static readonly Guid BranchA1Guid = new("a1a1a1a1-0000-0000-0000-0000000000a1");
    public static readonly Guid BranchA2Guid = new("a2a2a2a2-0000-0000-0000-0000000000a2");
    public static readonly Guid BranchB1Guid = new("b1b1b1b1-0000-0000-0000-0000000000b1");
    public static readonly Guid DeptA1Guid = new("d1d1d1d1-0000-0000-0000-0000000000a1");
    public static readonly Guid DeptA2Guid = new("d2d2d2d2-0000-0000-0000-0000000000a2");
    public static readonly Guid DeptB1Guid = new("d3d3d3d3-0000-0000-0000-0000000000b1");

    public static readonly TenantId TenantA = new(TenantAGuid);
    public static readonly TenantId TenantB = new(TenantBGuid);
    public static readonly BranchId BranchA1 = new(BranchA1Guid);
    public static readonly BranchId BranchA2 = new(BranchA2Guid);
    public static readonly BranchId BranchB1 = new(BranchB1Guid);
    public static readonly DepartmentId DeptA1 = new(DeptA1Guid);
    public static readonly DepartmentId DeptA2 = new(DeptA2Guid);
    public static readonly DepartmentId DeptB1 = new(DeptB1Guid);
}
