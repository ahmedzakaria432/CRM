using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Persistence;

public sealed class AuditingSaveChangesInterceptorTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LaterAt = new(2026, 10, 12, 15, 30, 0, TimeSpan.Zero);

    private static DepartmentRow Row(string name = "row") =>
        DepartmentRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, name);

    private static TestDbContext Agent(string database, DateTimeOffset at, string user = "user-1") =>
        TestDbContext.Create(
            database,
            CurrentUsers.Agent(Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1), user),
            Clock.Fixed(at));

    private static TestDbContext System(string database, DateTimeOffset at) =>
        TestDbContext.Create(database, CurrentUsers.Unrestricted(), Clock.Fixed(at));

    private static async Task<DepartmentRow> LoadAsync(string database, Guid id)
    {
        await using var store = TestDbContext.Store(database);
        return await store.DepartmentRows.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnAddedAuditableEntityGetsCreatedByAndCreatedAt()
    {
        var database = TestDbContext.NewDatabaseName();
        var row = Row();
        await using (var context = Agent(database, CreatedAt, "user-7"))
        {
            context.DepartmentRows.Add(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var saved = await LoadAsync(database, row.Id);
        Assert.Equal("user-7", saved.CreatedBy);
        Assert.Equal(CreatedAt, saved.CreatedAtUtc);
        Assert.Null(saved.ModifiedBy);
        Assert.Null(saved.ModifiedAtUtc);
    }

    [Fact]
    public async Task ACreationWithNoUserIsStampedAsTheSystem()
    {
        var database = TestDbContext.NewDatabaseName();
        var row = Row();
        await using (var context = System(database, CreatedAt))
        {
            context.DepartmentRows.Add(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal("system", (await LoadAsync(database, row.Id)).CreatedBy);
    }

    [Fact]
    public async Task AModifiedEntityGetsModifiedByAndModifiedAtAndKeepsItsCreationStamp()
    {
        var database = TestDbContext.NewDatabaseName();
        var row = Row();
        await using (var context = Agent(database, CreatedAt, "creator"))
        {
            context.DepartmentRows.Add(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = Agent(database, LaterAt, "editor"))
        {
            var tracked = await context.DepartmentRows.SingleAsync(r => r.Id == row.Id, TestContext.Current.CancellationToken);
            tracked.Rename("renamed");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var saved = await LoadAsync(database, row.Id);
        Assert.Equal("editor", saved.ModifiedBy);
        Assert.Equal(LaterAt, saved.ModifiedAtUtc);
        Assert.Equal("creator", saved.CreatedBy);
        Assert.Equal(CreatedAt, saved.CreatedAtUtc);
    }

    [Fact]
    public async Task AnUnchangedEntityIsNotStamped()
    {
        var database = TestDbContext.NewDatabaseName();
        var row = Row();
        await using (var context = Agent(database, CreatedAt))
        {
            context.DepartmentRows.Add(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = Agent(database, LaterAt, "someone-else"))
        {
            await context.DepartmentRows.SingleAsync(r => r.Id == row.Id, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var saved = await LoadAsync(database, row.Id);
        Assert.Null(saved.ModifiedBy);
        Assert.Null(saved.ModifiedAtUtc);
    }

    [Fact]
    public async Task RemovingASoftDeletableEntityMarksItDeletedAndKeepsTheRow()
    {
        var database = TestDbContext.NewDatabaseName();
        var row = Row();
        await using (var context = Agent(database, CreatedAt))
        {
            context.DepartmentRows.Add(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = Agent(database, LaterAt, "remover"))
        {
            var tracked = await context.DepartmentRows.SingleAsync(r => r.Id == row.Id, TestContext.Current.CancellationToken);
            context.DepartmentRows.Remove(tracked);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var saved = await LoadAsync(database, row.Id);
        Assert.True(saved.IsDeleted);
        Assert.Equal(LaterAt, saved.DeletedAtUtc);
        Assert.Equal("remover", saved.DeletedBy);
        Assert.Equal("row", saved.Name);
    }

    [Fact]
    public async Task ASoftDeletedRowIsAlsoStampedAsModified()
    {
        // Observed, and consistent with the interceptor's ordering comment: SoftDelete runs first, the entry becomes a
        // Modified update, and Audit then stamps it.
        var database = TestDbContext.NewDatabaseName();
        var row = Row();
        await using (var context = Agent(database, CreatedAt))
        {
            context.DepartmentRows.Add(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = Agent(database, LaterAt, "remover"))
        {
            var tracked = await context.DepartmentRows.SingleAsync(r => r.Id == row.Id, TestContext.Current.CancellationToken);
            context.DepartmentRows.Remove(tracked);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var saved = await LoadAsync(database, row.Id);
        Assert.Equal("remover", saved.ModifiedBy);
        Assert.Equal(LaterAt, saved.ModifiedAtUtc);
    }

    [Fact]
    public async Task ASoftDeletedRowStaysInvisibleToNormalQueries()
    {
        var database = TestDbContext.NewDatabaseName();
        var row = Row();
        await using (var context = Agent(database, CreatedAt))
        {
            context.DepartmentRows.Add(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            context.DepartmentRows.Remove(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var reader = Agent(database, LaterAt);
        Assert.Empty(await reader.DepartmentRows.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnEntityThatIsNotSoftDeletableIsPhysicallyDeleted()
    {
        var database = TestDbContext.NewDatabaseName();
        var row = UnscopedRow.Create(Guid.NewGuid(), "gone");
        await using (var context = Agent(database, CreatedAt))
        {
            context.UnscopedRows.Add(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            context.UnscopedRows.Remove(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var store = TestDbContext.Store(database);
        Assert.Equal(0, await store.UnscopedRows.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OwnedValueObjectsOfASoftDeletedRowSurvive()
    {
        var database = TestDbContext.NewDatabaseName();
        var contact = ContactRow.Create(Guid.NewGuid(), ScopeFixtures.TenantA, "Riyadh");
        await using (var context = Agent(database, CreatedAt))
        {
            context.ContactRows.Add(contact);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            context.ContactRows.Remove(contact);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var store = TestDbContext.Store(database);
        var saved = await store.ContactRows.IgnoreQueryFilters().SingleAsync(c => c.Id == contact.Id, TestContext.Current.CancellationToken);
        Assert.True(saved.IsDeleted);
        Assert.Equal("Riyadh", saved.Address.City);
    }
}
