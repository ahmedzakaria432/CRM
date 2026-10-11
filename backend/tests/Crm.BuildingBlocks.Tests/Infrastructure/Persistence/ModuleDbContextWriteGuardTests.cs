using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Persistence;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Persistence;

public sealed class ModuleDbContextWriteGuardTests
{
    private static OrganisationScope InA1() => Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1);

    private static TestDbContext As(string database, OrganisationScope scope) =>
        TestDbContext.Create(database, CurrentUsers.Agent(scope));

    private static DepartmentRow Row(string name, TenantId? tenant = null, BranchId? branch = null, DepartmentId? department = null) =>
        DepartmentRow.Create(
            Guid.NewGuid(),
            tenant ?? ScopeFixtures.TenantA,
            branch ?? ScopeFixtures.BranchA1,
            department ?? ScopeFixtures.DeptA1,
            name);

    private static async Task<int> CountAsync(string database)
    {
        await using var store = TestDbContext.Store(database);
        return await store.DepartmentRows.CountAsync(TestContext.Current.CancellationToken);
    }

    // --- add ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AddingARowInScopeSaves()
    {
        var database = TestDbContext.NewDatabaseName();
        await using (var context = As(database, InA1()))
        {
            context.DepartmentRows.Add(Row("mine"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, await CountAsync(database));
    }

    [Fact]
    public async Task AddingARowInAnotherDepartmentIsRefusedAndNothingIsPersisted()
    {
        var database = TestDbContext.NewDatabaseName();
        await using var context = As(database, InA1());
        context.DepartmentRows.Add(Row("in-scope"));
        context.DepartmentRows.Add(Row("theirs", department: ScopeFixtures.DeptA2, branch: ScopeFixtures.BranchA2));

        var exception = await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal("DepartmentRow", exception.EntityType);
        Assert.Equal("Write", exception.Operation);
        Assert.Equal(0, await CountAsync(database));
    }

    [Fact]
    public async Task AddingARowInAnotherBranchIsRefused()
    {
        var database = TestDbContext.NewDatabaseName();
        await using var context = As(database, InA1());
        context.DepartmentRows.Add(Row("other-branch", branch: ScopeFixtures.BranchA2));

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddingARowInAnotherTenantIsRefused()
    {
        var database = TestDbContext.NewDatabaseName();
        await using var context = As(database, InA1());
        context.DepartmentRows.Add(Row("other-tenant", tenant: ScopeFixtures.TenantB));

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await CountAsync(database));
    }

    [Fact]
    public async Task AnUnrestrictedScopeMayWriteAnywhere()
    {
        var database = TestDbContext.NewDatabaseName();
        await using (var store = TestDbContext.Store(database))
        {
            store.DepartmentRows.Add(Row("anywhere", tenant: ScopeFixtures.TenantB, branch: ScopeFixtures.BranchB1, department: ScopeFixtures.DeptB1));
            await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, await CountAsync(database));
    }

    [Fact]
    public async Task TheNoneScopeIsRefusedAnyScopedWrite()
    {
        var database = TestDbContext.NewDatabaseName();
        await using var context = TestDbContext.Create(database, CurrentUsers.Anonymous());
        context.DepartmentRows.Add(Row("nobody"));

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnEntityWithNoScopeMarkerIsNeverGuarded()
    {
        var database = TestDbContext.NewDatabaseName();
        await using var context = TestDbContext.Create(database, CurrentUsers.Anonymous());
        context.UnscopedRows.Add(UnscopedRow.Create(Guid.NewGuid(), "free"));

        var saved = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, saved);
    }

    // --- modify -------------------------------------------------------------------------------------------

    private static async Task<Guid> SeedRowAsync(string database, string name = "seed")
    {
        await using var store = TestDbContext.Store(database);
        var row = Row(name);
        store.DepartmentRows.Add(row);
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        return row.Id;
    }

    [Fact]
    public async Task ModifyingARowInScopeSaves()
    {
        var database = TestDbContext.NewDatabaseName();
        var id = await SeedRowAsync(database);
        await using (var context = As(database, InA1()))
        {
            var row = await context.DepartmentRows.SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken);
            row.Rename("renamed");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var store = TestDbContext.Store(database);
        Assert.Equal("renamed", (await store.DepartmentRows.SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken)).Name);
    }

    [Fact]
    public async Task ModifyingARowSoThatItLeavesTheCallersScopeIsRefused()
    {
        var database = TestDbContext.NewDatabaseName();
        var id = await SeedRowAsync(database);
        await using var context = As(database, InA1());
        var row = await context.DepartmentRows.SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken);

        row.MoveTo(ScopeFixtures.BranchA2, ScopeFixtures.DeptA2);

        var exception = await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Write", exception.Operation);
    }

    // --- transfer -----------------------------------------------------------------------------------------

    private static async Task<Guid> SeedTransferableAsync(string database)
    {
        await using var store = TestDbContext.Store(database);
        var row = TransferableRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1);
        store.TransferableRows.Add(row);
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        return row.Id;
    }

    [Fact]
    public async Task ATransferableRowMayBeHandedToAnotherDepartmentOfTheSameTenantByItsOwner()
    {
        var database = TestDbContext.NewDatabaseName();
        var id = await SeedTransferableAsync(database);
        await using (var context = As(database, InA1()))
        {
            var row = await context.TransferableRows.SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken);
            row.TransferTo(ScopeFixtures.BranchA2, ScopeFixtures.DeptA2);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var store = TestDbContext.Store(database);
        var saved = await store.TransferableRows.SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(ScopeFixtures.DeptA2, saved.DepartmentId);
        Assert.Equal(ScopeFixtures.BranchA2, saved.BranchId);
    }

    [Fact]
    public async Task ATransferByACallerWhoDidNotOwnTheOriginalDepartmentIsRefusedWhenTheDestinationIsOutOfScope()
    {
        var database = TestDbContext.NewDatabaseName();
        var id = await SeedTransferableAsync(database);
        await using var context = As(database, Scopes.AgentIn(ScopeFixtures.DeptA2, ScopeFixtures.BranchA2));
        // Production never loads an out-of-scope row; the test lifts the filter to reach the guard.
        var row = await context.TransferableRows.IgnoreQueryFilters().SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken);

        row.TransferTo(ScopeFixtures.BranchB1, ScopeFixtures.DeptB1);

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ATransferToAnotherTenantIsRefused()
    {
        var database = TestDbContext.NewDatabaseName();
        var id = await SeedTransferableAsync(database);
        await using var context = As(database, InA1());
        var row = await context.TransferableRows.SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken);

        row.MoveToTenant(ScopeFixtures.TenantB);

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnAddedTransferableRowIsJudgedLikeAnyOther()
    {
        var database = TestDbContext.NewDatabaseName();
        await using var context = As(database, InA1());
        context.TransferableRows.Add(
            TransferableRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2));

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // --- portal -------------------------------------------------------------------------------------------

    [Fact]
    public async Task APortalCustomerMayWriteAnOwnedRowForItsOwnCustomer()
    {
        var database = TestDbContext.NewDatabaseName();
        await using (var context = TestDbContext.Create(
                         database, CurrentUsers.Portal(Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid))))
        {
            context.OwnedRows.Add(OwnedRow.Create(
                Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, Scopes.PortalCustomerGuid));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var store = TestDbContext.Store(database);
        Assert.Equal(1, await store.OwnedRows.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task APortalCustomerMayNotWriteAnOwnedRowForAnotherCustomer()
    {
        var database = TestDbContext.NewDatabaseName();
        await using var context = TestDbContext.Create(
            database, CurrentUsers.Portal(Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid)));
        context.OwnedRows.Add(OwnedRow.Create(
            Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, Seed.OtherCustomerGuid));

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // --- delete -------------------------------------------------------------------------------------------

    [Fact]
    public async Task ADeletedEntryIsNotGuarded()
    {
        // Pins the current behaviour: the guard looks only at Added and Modified entries, and a Remove of a tracked
        // soft-deletable row is still Deleted when the guard runs (the interceptor turns it into an update afterwards).
        // Unreachable through the repositories, which never load an out-of-scope row. Recorded as a Follow-up.
        var database = TestDbContext.NewDatabaseName();
        var id = await SeedRowAsync(database, "theirs");
        await using var context = As(database, Scopes.AgentIn(ScopeFixtures.DeptA2, ScopeFixtures.BranchA2));
        var row = await context.DepartmentRows.IgnoreQueryFilters().SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken);

        context.DepartmentRows.Remove(row);
        var saved = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(saved > 0);
    }
}
