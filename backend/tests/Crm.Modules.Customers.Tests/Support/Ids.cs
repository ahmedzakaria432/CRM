using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Customers.Tests.Support;

/// <summary>Fixed ids so a failing assertion names the same row every run. Tenant A is the caller, tenant B is someone else.</summary>
public static class Ids
{
    public static readonly TenantId TenantA = new(new Guid("aaaaaaaa-0000-0000-0000-00000000000a"));
    public static readonly TenantId TenantB = new(new Guid("bbbbbbbb-0000-0000-0000-00000000000b"));
    public static readonly BranchId BranchA1 = new(new Guid("a1a1a1a1-0000-0000-0000-0000000000a1"));
    public static readonly BranchId BranchA2 = new(new Guid("a2a2a2a2-0000-0000-0000-0000000000a2"));
    public static readonly BranchId BranchB1 = new(new Guid("b1b1b1b1-0000-0000-0000-0000000000b1"));
    public static readonly DepartmentId DepartmentA1 = new(new Guid("d1d1d1d1-0000-0000-0000-0000000000a1"));
    public static readonly DepartmentId DepartmentA2 = new(new Guid("d2d2d2d2-0000-0000-0000-0000000000a2"));
    public static readonly DepartmentId DepartmentB1 = new(new Guid("d3d3d3d3-0000-0000-0000-0000000000b1"));

    public static readonly CustomerId Customer1 = new(new Guid("c1c1c1c1-0000-0000-0000-000000000001"));
    public static readonly CustomerId Customer2 = new(new Guid("c1c1c1c1-0000-0000-0000-000000000002"));
    public static readonly CustomerId Customer3 = new(new Guid("c1c1c1c1-0000-0000-0000-000000000003"));

    public static readonly Guid Contact1 = new("c0c0c0c0-0000-0000-0000-000000000001");
    public static readonly Guid Contact2 = new("c0c0c0c0-0000-0000-0000-000000000002");
    public static readonly Guid Contact3 = new("c0c0c0c0-0000-0000-0000-000000000003");
    public static readonly Guid Contact4 = new("c0c0c0c0-0000-0000-0000-000000000004");

    public static readonly Guid Note1 = new("a0a0a0a0-0000-0000-0000-000000000001");
    public static readonly Guid Note2 = new("a0a0a0a0-0000-0000-0000-000000000002");
    public static readonly Guid Note3 = new("a0a0a0a0-0000-0000-0000-000000000003");

    public static readonly Guid Author = new("0e0e0e0e-0000-0000-0000-000000000001");
    public static readonly Guid Missing = new("deadbeef-0000-0000-0000-000000000000");

    public static byte[] RowVersion() => [1, 2, 3, 4, 5, 6, 7, 8];

    public static byte[] OtherRowVersion() => [8, 7, 6, 5, 4, 3, 2, 1];
}
