using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Persistence;

public sealed class ModuleDbContextScopeFilterTests
{
    private static readonly ScopeMembership AgentA1 = Scopes.Member(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1);
    private static readonly ScopeMembership AgentA2 = Scopes.Member(ScopeFixtures.DeptA2, ScopeFixtures.BranchA2);

    private static async Task<string> SeededAsync()
    {
        var database = TestDbContext.NewDatabaseName();
        await Seed.StandardAsync(database);
        return database;
    }

    private static TestDbContext As(string database, OrganisationScope scope) =>
        TestDbContext.Create(database, CurrentUsers.Agent(scope));

    [Fact]
    public async Task AnAgentSeesOnlyTheRowsOfItsOwnBranchAndDepartment()
    {
        var database = await SeededAsync();
        await using var context = As(database, Scopes.Agent(AgentA1));

        Assert.Equal(["A1-A1"], await Seed.DepartmentNamesAsync(context));
    }

    [Fact]
    public async Task AnotherTenantsRowsAreHidden()
    {
        var database = await SeededAsync();
        await using var context = As(database, Scopes.AgentOfTenant(ScopeFixtures.TenantB, Scopes.Member(ScopeFixtures.DeptB1, ScopeFixtures.BranchB1)));

        Assert.Equal(["B-B"], await Seed.DepartmentNamesAsync(context));
    }

    [Fact]
    public async Task ARowOfAnotherDepartmentOrBranchIsHidden()
    {
        var database = await SeededAsync();
        await using var context = As(database, Scopes.Agent(AgentA1));

        var names = await Seed.DepartmentNamesAsync(context);

        Assert.DoesNotContain("A2-A2", names);
        Assert.DoesNotContain("A1-A2", names);
        Assert.DoesNotContain("A2-A1", names);
    }

    [Fact]
    public async Task TwoDepartmentsSeeBothAndTheRuleIsColumnWiseInSqlToo()
    {
        var database = await SeededAsync();
        await using var context = As(database, Scopes.Agent(AgentA1, AgentA2));

        // Branch and department are each tested against the caller's sets, so the mixed pairs are visible as well.
        Assert.Equal(["A1-A1", "A1-A2", "A2-A1", "A2-A2"], await Seed.DepartmentNamesAsync(context));
    }

    [Fact]
    public async Task ANarrowedScopeSeesOnlyTheActiveDepartment()
    {
        var database = await SeededAsync();
        await using var context = As(database, Scopes.Narrowed([AgentA1, AgentA2], ScopeFixtures.DeptA2));

        Assert.Equal(["A2-A2"], await Seed.DepartmentNamesAsync(context));
    }

    [Fact]
    public async Task ARejectedScopeSeesNoDepartmentRow()
    {
        var database = await SeededAsync();
        await using var context = As(database, Scopes.Rejected([AgentA1, AgentA2]));

        Assert.Empty(await Seed.DepartmentNamesAsync(context));
        // Tenant-scoped rows need only the tenant, which a rejected scope keeps (the middleware answers 403 anyway).
        Assert.Equal(["tenant-a"], await Seed.TenantNamesAsync(context));
    }

    [Fact]
    public async Task TheNoneScopeSeesNoScopedRow()
    {
        var database = await SeededAsync();
        await using var context = TestDbContext.Create(database, CurrentUsers.Anonymous());

        Assert.Empty(await Seed.DepartmentNamesAsync(context));
        Assert.Empty(await Seed.TenantNamesAsync(context));
    }

    [Fact]
    public async Task AnUnrestrictedScopeSeesEverything()
    {
        var database = await SeededAsync();
        await using var context = TestDbContext.Store(database);

        Assert.Equal(5, (await Seed.DepartmentNamesAsync(context)).Count);
        Assert.Equal(["tenant-a", "tenant-b"], await Seed.TenantNamesAsync(context));
    }

    [Fact]
    public async Task ATenantScopedRowIsVisibleToEveryDepartmentOfItsTenantAndNoOneElse()
    {
        var database = await SeededAsync();
        await using var inA1 = As(database, Scopes.Agent(AgentA1));
        await using var inA2 = As(database, Scopes.Agent(AgentA2));
        await using var inB = As(database, Scopes.AgentOfTenant(ScopeFixtures.TenantB, Scopes.Member(ScopeFixtures.DeptB1, ScopeFixtures.BranchB1)));

        Assert.Equal(["tenant-a"], await Seed.TenantNamesAsync(inA1));
        Assert.Equal(["tenant-a"], await Seed.TenantNamesAsync(inA2));
        Assert.Equal(["tenant-b"], await Seed.TenantNamesAsync(inB));
    }

    [Fact]
    public async Task ARowWithNoScopeMarkerIsVisibleToEveryone()
    {
        var database = await SeededAsync();
        await using var anonymous = TestDbContext.Create(database, CurrentUsers.Anonymous());
        await using var agent = As(database, Scopes.Agent(AgentA1));
        await using var rejected = As(database, Scopes.Rejected([AgentA1]));

        Assert.Single(await anonymous.UnscopedRows.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await agent.UnscopedRows.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await rejected.UnscopedRows.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task APortalCustomerSeesItsOwnCustomersRowAndTheTenantScopedRowsOfItsTenant()
    {
        var database = await SeededAsync();
        await using var context = TestDbContext.Create(
            database, CurrentUsers.Portal(Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid)));

        var owned = await context.OwnedRows.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Scopes.PortalCustomerGuid, Assert.Single(owned).CustomerId);
        Assert.Equal(["tenant-a"], await Seed.TenantNamesAsync(context));
    }

    [Fact]
    public async Task APortalCustomerNeverSeesAnotherCustomersOrAnotherTenantsRow()
    {
        var database = await SeededAsync();
        await using var context = TestDbContext.Create(
            database, CurrentUsers.Portal(Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid)));

        var owned = await context.OwnedRows.ToListAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(owned, r => r.CustomerId == Seed.OtherCustomerGuid);
        Assert.DoesNotContain(owned, r => r.TenantId == ScopeFixtures.TenantB);
    }

    [Fact]
    public async Task APortalCustomerSeesNoOperationalRowThatIsNotCustomerOwned()
    {
        var database = await SeededAsync();
        await using var context = TestDbContext.Create(
            database, CurrentUsers.Portal(Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid)));

        Assert.Empty(await Seed.DepartmentNamesAsync(context));
    }

    [Fact]
    public async Task AStaffAgentSeesEveryCustomersOwnedRowsOfItsDepartment()
    {
        var database = await SeededAsync();
        await using var context = As(database, Scopes.Agent(AgentA1, AgentA2));

        var owned = await context.OwnedRows.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, owned.Count);
        Assert.DoesNotContain(owned, r => r.TenantId == ScopeFixtures.TenantB);
    }

    // --- soft delete --------------------------------------------------------------------------------------

    private static async Task SoftDeleteAsync(string database, string name)
    {
        await using var store = TestDbContext.Store(database);
        var row = await store.DepartmentRows.SingleAsync(r => r.Name == name, TestContext.Current.CancellationToken);
        store.DepartmentRows.Remove(row);
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ASoftDeletedRowIsHiddenByDefault()
    {
        var database = await SeededAsync();
        await SoftDeleteAsync(database, "A1-A1");
        await using var context = As(database, Scopes.Agent(AgentA1));

        Assert.Empty(await Seed.DepartmentNamesAsync(context));
    }

    [Fact]
    public async Task LiftingOnlyTheSoftDeleteFilterShowsDeletedRowsButKeepsTheScopeFilter()
    {
        var database = await SeededAsync();
        await SoftDeleteAsync(database, "A1-A1");
        await SoftDeleteAsync(database, "A2-A2");
        await using var context = As(database, Scopes.Agent(AgentA1));

        var names = await context.DepartmentRows
            .IgnoreQueryFilters([ModuleDbContext.SoftDeleteFilterName])
            .Select(r => r.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["A1-A1"], names);
    }

    [Fact]
    public async Task LiftingOnlyTheScopeFilterShowsOtherDepartmentsButNeverDeletedRows()
    {
        var database = await SeededAsync();
        await SoftDeleteAsync(database, "B-B");
        await using var context = As(database, Scopes.Agent(AgentA1));

        var names = await context.DepartmentRows
            .IgnoreQueryFilters([ModuleDbContext.ScopeFilterName])
            .Select(r => r.Name)
            .OrderBy(n => n)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["A1-A1", "A1-A2", "A2-A1", "A2-A2"], names);
    }

    [Fact]
    public void TheFilterNamesAreStable()
    {
        Assert.Equal("OrganisationScope", ModuleDbContext.ScopeFilterName);
        Assert.Equal("SoftDelete", ModuleDbContext.SoftDeleteFilterName);
    }

    [Fact]
    public async Task TheScopeIsReadFromEachContextsOwnCurrentUser()
    {
        var database = await SeededAsync();
        await using var inA1 = As(database, Scopes.Agent(AgentA1));
        await using var inA2 = As(database, Scopes.Agent(AgentA2));

        Assert.Equal(["A1-A1"], await Seed.DepartmentNamesAsync(inA1));
        Assert.Equal(["A2-A2"], await Seed.DepartmentNamesAsync(inA2));
        Assert.Equal(["A1-A1"], await Seed.DepartmentNamesAsync(inA1));
    }
}
