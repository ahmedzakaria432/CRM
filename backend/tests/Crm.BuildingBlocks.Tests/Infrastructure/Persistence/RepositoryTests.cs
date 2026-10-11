using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Persistence;
using Crm.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Persistence;

public sealed class RepositoryTests
{
    private static readonly Guid MineId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TheirsId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid GoneId = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid UnscopedId = new("00000000-0000-0000-0000-000000000004");

    private static async Task<string> SeededAsync()
    {
        var database = TestDbContext.NewDatabaseName();
        await using var store = TestDbContext.Store(database);
        store.DepartmentRows.AddRange(
            DepartmentRow.Create(MineId, ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, "mine"),
            DepartmentRow.Create(TheirsId, ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2, "theirs"),
            DepartmentRow.Create(GoneId, ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, "gone"));
        store.UnscopedRows.Add(UnscopedRow.Create(UnscopedId, "unscoped"));
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);

        var gone = await store.DepartmentRows.SingleAsync(r => r.Id == GoneId, TestContext.Current.CancellationToken);
        store.DepartmentRows.Remove(gone);
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        return database;
    }

    private static (TestDbContext Context, Repository<DepartmentRow, Guid, TestDbContext> Repository) AgentInA1(string database)
    {
        var context = TestDbContext.Create(database, CurrentUsers.Agent(Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1)));
        return (context, new Repository<DepartmentRow, Guid, TestDbContext>(context));
    }

    private static ConfigurableSpec<DepartmentRow> Named(string name) => new ConfigurableSpec<DepartmentRow>().Filter(r => r.Name == name);

    // --- GetByIdAsync -------------------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdReturnsARowInScope()
    {
        var (context, repository) = AgentInA1(await SeededAsync());
        await using var _ = context;

        var row = await repository.GetByIdAsync(MineId, TestContext.Current.CancellationToken);

        Assert.Equal("mine", row?.Name);
    }

    [Fact]
    public async Task GetByIdOfAMissingIdIsNull()
    {
        var (context, repository) = AgentInA1(await SeededAsync());
        await using var _ = context;

        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetByIdOfARowOutsideScopeIsAForbiddenNotAnAccidentalNull()
    {
        var (context, repository) = AgentInA1(await SeededAsync());
        await using var _ = context;

        var exception = await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => repository.GetByIdAsync(TheirsId, TestContext.Current.CancellationToken));

        Assert.Equal("DepartmentRow", exception.EntityType);
        Assert.Equal("Read", exception.Operation);
    }

    [Fact]
    public async Task GetByIdOfASoftDeletedRowIsNull()
    {
        var (context, repository) = AgentInA1(await SeededAsync());
        await using var _ = context;

        Assert.Null(await repository.GetByIdAsync(GoneId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetByIdOfAnUnscopedAggregateNeverThrows()
    {
        var database = await SeededAsync();
        await using var context = TestDbContext.Create(database, CurrentUsers.Anonymous());
        var repository = new Repository<UnscopedRow, Guid, TestDbContext>(context);

        Assert.Equal("unscoped", (await repository.GetByIdAsync(UnscopedId, TestContext.Current.CancellationToken))?.Name);
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    // --- FirstOrDefaultAsync ------------------------------------------------------------------------------

    [Fact]
    public async Task FirstOrDefaultReturnsAMatchInScope()
    {
        var (context, repository) = AgentInA1(await SeededAsync());
        await using var _ = context;

        Assert.Equal(MineId, (await repository.FirstOrDefaultAsync(Named("mine"), TestContext.Current.CancellationToken))?.Id);
    }

    [Fact]
    public async Task FirstOrDefaultWithNoMatchIsNull()
    {
        var (context, repository) = AgentInA1(await SeededAsync());
        await using var _ = context;

        Assert.Null(await repository.FirstOrDefaultAsync(Named("nobody"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FirstOrDefaultOfAMatchOutsideScopeIsForbidden()
    {
        var (context, repository) = AgentInA1(await SeededAsync());
        await using var _ = context;

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => repository.FirstOrDefaultAsync(Named("theirs"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FirstOrDefaultOfASoftDeletedMatchIsNull()
    {
        var (context, repository) = AgentInA1(await SeededAsync());
        await using var _ = context;

        Assert.Null(await repository.FirstOrDefaultAsync(Named("gone"), TestContext.Current.CancellationToken));
    }

    // --- ListAsync and CountAsync -------------------------------------------------------------------------

    private static async Task<string> ManyAsync()
    {
        var database = TestDbContext.NewDatabaseName();
        await using var store = TestDbContext.Store(database);
        foreach (string name in new[] { "d", "b", "a", "c" })
        {
            store.DepartmentRows.Add(DepartmentRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, name));
        }

        store.DepartmentRows.Add(DepartmentRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2, "other-department"));
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        return database;
    }

    [Fact]
    public async Task ListAppliesCriteriaOrderingPagingAndScope()
    {
        var (context, repository) = AgentInA1(await ManyAsync());
        await using var _ = context;
        var spec = new ConfigurableSpec<DepartmentRow>().Order(r => r.Name).Page(1, 2);

        var rows = await repository.ListAsync(spec, TestContext.Current.CancellationToken);

        Assert.Equal(["b", "c"], rows.Select(r => r.Name));
    }

    [Fact]
    public async Task ListWithCriteriaFilters()
    {
        var (context, repository) = AgentInA1(await ManyAsync());
        await using var _ = context;
        var spec = new ConfigurableSpec<DepartmentRow>().Filter(r => r.Name != "a").Order(r => r.Name);

        Assert.Equal(["b", "c", "d"], (await repository.ListAsync(spec, TestContext.Current.CancellationToken)).Select(r => r.Name));
    }

    [Fact]
    public async Task ListNeverReturnsAnotherDepartmentsRows()
    {
        var (context, repository) = AgentInA1(await ManyAsync());
        await using var _ = context;

        var rows = await repository.ListAsync(new ConfigurableSpec<DepartmentRow>(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(rows, r => r.Name == "other-department");
        Assert.Equal(4, rows.Count);
    }

    [Fact]
    public async Task CountAppliesTheSameFilters()
    {
        var (context, repository) = AgentInA1(await ManyAsync());
        await using var _ = context;

        Assert.Equal(4, await repository.CountAsync(new ConfigurableSpec<DepartmentRow>(), TestContext.Current.CancellationToken));
        Assert.Equal(1, await repository.CountAsync(Named("c"), TestContext.Current.CancellationToken));
        Assert.Equal(0, await repository.CountAsync(Named("other-department"), TestContext.Current.CancellationToken));
    }

    // --- Add, Remove, row version -------------------------------------------------------------------------

    [Fact]
    public async Task AddThenSavePersists()
    {
        var database = TestDbContext.NewDatabaseName();
        var (context, repository) = AgentInA1(database);
        await using (context)
        {
            repository.Add(DepartmentRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, "added"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var store = TestDbContext.Store(database);
        Assert.Equal(1, await store.DepartmentRows.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveSoftDeletesTheRow()
    {
        var database = await SeededAsync();
        var (context, repository) = AgentInA1(database);
        await using (context)
        {
            var row = await repository.GetByIdAsync(MineId, TestContext.Current.CancellationToken);
            Assert.NotNull(row);
            repository.Remove(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var store = TestDbContext.Store(database);
        Assert.Null(await store.DepartmentRows.SingleOrDefaultAsync(r => r.Id == MineId, TestContext.Current.CancellationToken));
        var stored = await store.DepartmentRows.IgnoreQueryFilters().SingleAsync(r => r.Id == MineId, TestContext.Current.CancellationToken);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task SetOriginalRowVersionWritesTheOriginalValueOfTheRowVersionProperty()
    {
        var (context, repository) = AgentInA1(await SeededAsync());
        await using var _ = context;
        var row = await repository.GetByIdAsync(MineId, TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        byte[] expected = [1, 2, 3, 4];

        repository.SetOriginalRowVersion(row, expected);

        Assert.Equal(expected, context.Entry(row).Property(nameof(IHasRowVersion.RowVersion)).OriginalValue);
    }
}
