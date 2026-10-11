using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Persistence;

public sealed class SpecificationEvaluatorTests
{
    private static Guid Id(int n) => new($"00000000-0000-0000-0000-00000000000{n}");

    /// <summary>Rows (name, id): ("b",3) ("a",2) ("a",1) in department A1, one soft-deleted ("deleted",4) in A1 and one live
    /// ("elsewhere",5) in department A2.</summary>
    private static async Task<string> SeededAsync()
    {
        var database = TestDbContext.NewDatabaseName();
        await using var store = TestDbContext.Store(database);
        store.DepartmentRows.AddRange(
            DepartmentRow.Create(Id(3), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, "b"),
            DepartmentRow.Create(Id(2), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, "a"),
            DepartmentRow.Create(Id(1), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, "a"),
            DepartmentRow.Create(Id(4), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, "deleted"),
            DepartmentRow.Create(Id(5), ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2, "elsewhere"));
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        store.DepartmentRows.Remove(await store.DepartmentRows.SingleAsync(r => r.Id == Id(4), TestContext.Current.CancellationToken));
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        return database;
    }

    private static TestDbContext Agent(string database) =>
        TestDbContext.Create(database, CurrentUsers.Agent(Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1)));

    private static Task<List<DepartmentRow>> RunAsync(TestDbContext context, ConfigurableSpec<DepartmentRow> spec) =>
        SpecificationEvaluator.GetQuery(context.DepartmentRows, spec).ToListAsync(TestContext.Current.CancellationToken);

    private static List<int> Ids(IEnumerable<DepartmentRow> rows) =>
        [.. rows.Select(r => int.Parse(r.Id.ToString("N")[^1..], System.Globalization.CultureInfo.InvariantCulture))];

    [Fact]
    public async Task AnEmptySpecificationLeavesTheQueryUnchangedExceptForTheFilters()
    {
        await using var context = Agent(await SeededAsync());

        var rows = await RunAsync(context, new ConfigurableSpec<DepartmentRow>());

        Assert.Equal([1, 2, 3], Ids(rows).Order());
    }

    [Fact]
    public async Task CriteriaFilter()
    {
        await using var context = Agent(await SeededAsync());

        var rows = await RunAsync(context, new ConfigurableSpec<DepartmentRow>().Filter(r => r.Name == "a"));

        Assert.Equal([1, 2], Ids(rows).Order());
    }

    [Fact]
    public async Task OrderByWithThenByOrdersByBothKeys()
    {
        await using var context = Agent(await SeededAsync());

        var rows = await RunAsync(context, new ConfigurableSpec<DepartmentRow>().Order(r => r.Name).Then(r => r.Id));

        Assert.Equal([1, 2, 3], Ids(rows));
    }

    [Fact]
    public async Task OrderByDescendingWithThenByAscendingOrdersByBothKeys()
    {
        await using var context = Agent(await SeededAsync());

        var rows = await RunAsync(context, new ConfigurableSpec<DepartmentRow>().OrderDescending(r => r.Name).Then(r => r.Id));

        Assert.Equal([3, 1, 2], Ids(rows));
    }

    [Fact]
    public async Task OrderByWinsWhenBothDirectionsAreSet()
    {
        await using var context = Agent(await SeededAsync());

        var rows = await RunAsync(context, new ConfigurableSpec<DepartmentRow>().OrderDescending(r => r.Name).Order(r => r.Name).Then(r => r.Id));

        Assert.Equal([1, 2, 3], Ids(rows));
    }

    [Fact]
    public async Task SkipAndTakeApplyAfterOrdering()
    {
        await using var context = Agent(await SeededAsync());

        var rows = await RunAsync(context, new ConfigurableSpec<DepartmentRow>().Order(r => r.Name).Then(r => r.Id).Page(1, 1));

        Assert.Equal([2], Ids(rows));
    }

    [Fact]
    public async Task AsReadOnlyReturnsUntrackedEntities()
    {
        await using var context = Agent(await SeededAsync());

        await RunAsync(context, new ConfigurableSpec<DepartmentRow>().ReadOnly());

        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task EntitiesAreTrackedByDefault()
    {
        await using var context = Agent(await SeededAsync());

        await RunAsync(context, new ConfigurableSpec<DepartmentRow>());

        Assert.Equal(3, context.ChangeTracker.Entries().Count());
    }

    [Fact]
    public async Task IncludeSoftDeletedShowsDeletedRowsButKeepsTheScopeFilter()
    {
        await using var context = Agent(await SeededAsync());

        var rows = await RunAsync(context, new ConfigurableSpec<DepartmentRow>().WithDeleted());

        Assert.Equal([1, 2, 3, 4], Ids(rows).Order());
        Assert.DoesNotContain(rows, r => r.Name == "elsewhere");
    }

    [Fact]
    public async Task IgnoreOrganisationScopeShowsOtherDepartmentsButStillHidesDeletedRows()
    {
        await using var context = Agent(await SeededAsync());

        var rows = await RunAsync(context, new ConfigurableSpec<DepartmentRow>().AcrossScopes());

        Assert.Equal([1, 2, 3, 5], Ids(rows).Order());
        Assert.DoesNotContain(rows, r => r.Name == "deleted");
    }

    [Fact]
    public async Task BothLiftsTogetherShowEverything()
    {
        await using var context = Agent(await SeededAsync());

        var rows = await RunAsync(context, new ConfigurableSpec<DepartmentRow>().WithDeleted().AcrossScopes());

        Assert.Equal([1, 2, 3, 4, 5], Ids(rows).Order());
    }

    [Fact]
    public async Task ASpecificationWithNoIncludesAddsNone()
    {
        var spec = new ConfigurableSpec<DepartmentRow>();

        Assert.Empty(spec.Includes);
        await using var context = Agent(await SeededAsync());
        Assert.NotEmpty(await RunAsync(context, spec));
    }
}
