using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;

namespace Crm.BuildingBlocks.Tests.Support.Persistence;

/// <summary>The standard data set: five department rows (the last two are mixed branch/department pairs and another
/// tenant), tenant rows of both tenants, owned rows of two customers, and one unscoped row.</summary>
public static class Seed
{
    public static readonly Guid OtherCustomerGuid = new("cccccccc-0000-0000-0000-0000000000c9");

    public static async Task StandardAsync(string database)
    {
        await using var store = TestDbContext.Store(database);

        store.DepartmentRows.AddRange(
            DepartmentRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, "A1-A1"),
            DepartmentRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2, "A2-A2"),
            DepartmentRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA2, "A1-A2"),
            DepartmentRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA1, "A2-A1"),
            DepartmentRow.Create(Guid.NewGuid(), ScopeFixtures.TenantB, ScopeFixtures.BranchB1, ScopeFixtures.DeptB1, "B-B"));
        store.TenantRows.AddRange(
            TenantRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, "tenant-a"),
            TenantRow.Create(Guid.NewGuid(), ScopeFixtures.TenantB, "tenant-b"));
        store.OwnedRows.AddRange(
            OwnedRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, Scopes.PortalCustomerGuid),
            OwnedRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2, OtherCustomerGuid),
            OwnedRow.Create(Guid.NewGuid(), ScopeFixtures.TenantB, ScopeFixtures.BranchB1, ScopeFixtures.DeptB1, Scopes.PortalCustomerGuid));
        store.UnscopedRows.Add(UnscopedRow.Create(Guid.NewGuid(), "everyone"));

        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<List<string>> DepartmentNamesAsync(TestDbContext context) =>
        await context.DepartmentRows.Select(r => r.Name).OrderBy(n => n).ToListAsync(TestContext.Current.CancellationToken);

    public static async Task<List<string>> TenantNamesAsync(TestDbContext context) =>
        await context.TenantRows.Select(r => r.Name).OrderBy(n => n).ToListAsync(TestContext.Current.CancellationToken);
}
