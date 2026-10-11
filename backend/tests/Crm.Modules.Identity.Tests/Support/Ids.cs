using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Tests.Support;

/// <summary>Fixed ids so a failing assertion names the same row every run. Tenant A is the caller, tenant B is someone else.</summary>
public static class Ids
{
    public static readonly TenantId TenantA = new(new Guid("aaaaaaaa-0000-0000-0000-00000000000a"));
    public static readonly TenantId TenantB = new(new Guid("bbbbbbbb-0000-0000-0000-00000000000b"));
    public static readonly BranchId BranchA1 = new(new Guid("a1a1a1a1-0000-0000-0000-0000000000a1"));
    public static readonly BranchId BranchA2 = new(new Guid("a2a2a2a2-0000-0000-0000-0000000000a2"));
    public static readonly DepartmentId DeptA1 = new(new Guid("d1d1d1d1-0000-0000-0000-0000000000a1"));
    public static readonly DepartmentId DeptA2 = new(new Guid("d2d2d2d2-0000-0000-0000-0000000000a2"));
    public static readonly Guid User = new("0e0e0e0e-0000-0000-0000-000000000001");
    public static readonly Guid OtherUser = new("0e0e0e0e-0000-0000-0000-000000000002");
    public static readonly Guid Session = new("5e5e5e5e-0000-0000-0000-000000000001");
    public static readonly Guid Family = new("fafafafa-0000-0000-0000-000000000001");
    public static readonly Guid Account = new("acacacac-0000-0000-0000-000000000001");
    public static readonly Guid Customer = new("c1c1c1c1-0000-0000-0000-000000000001");
    public static readonly Guid OtherCustomer = new("c1c1c1c1-0000-0000-0000-000000000002");
    public static readonly Guid Contact = new("c0c0c0c0-0000-0000-0000-000000000001");
    public static readonly Guid OtherContact = new("c0c0c0c0-0000-0000-0000-000000000002");

    public static byte[] RowVersion() => [1, 2, 3, 4, 5, 6, 7, 8];

    public static byte[] OtherRowVersion() => [8, 7, 6, 5, 4, 3, 2, 1];
}
