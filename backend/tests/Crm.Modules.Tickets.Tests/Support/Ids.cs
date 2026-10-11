using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Tests.Support;

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

    public static readonly Guid Customer1 = new("c1c1c1c1-0000-0000-0000-000000000001");
    public static readonly Guid Customer2 = new("c1c1c1c1-0000-0000-0000-000000000002");

    public static readonly Guid Contact1 = new("c0c0c0c0-0000-0000-0000-000000000001");
    public static readonly Guid Contact2 = new("c0c0c0c0-0000-0000-0000-000000000002");

    public static readonly Guid Agent1 = new("a9e0a9e0-0000-0000-0000-000000000001");
    public static readonly Guid Agent2 = new("a9e0a9e0-0000-0000-0000-000000000002");
    public static readonly Guid Actor = new("0e0e0e0e-0000-0000-0000-000000000001");

    public static readonly Guid Ticket1 = new("71c4e700-0000-0000-0000-000000000001");
    public static readonly Guid Ticket2 = new("71c4e700-0000-0000-0000-000000000002");
    public static readonly Guid Ticket3 = new("71c4e700-0000-0000-0000-000000000003");

    public static readonly Guid Category1 = new("ca7e0000-0000-0000-0000-000000000001");
    public static readonly Guid Category2 = new("ca7e0000-0000-0000-0000-000000000002");
    public static readonly Guid Category3 = new("ca7e0000-0000-0000-0000-000000000003");

    public static readonly Guid Missing = new("deadbeef-0000-0000-0000-000000000000");

    public static byte[] RowVersion() => [1, 2, 3, 4, 5, 6, 7, 8];

    public static byte[] OtherRowVersion() => [8, 7, 6, 5, 4, 3, 2, 1];
}
