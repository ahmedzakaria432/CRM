using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;

namespace Crm.BuildingBlocks.Tests.Support.Persistence;

/// <summary>A module DbContext over the EF Core InMemory provider, in the schema "test". Two requests over one store
/// are two contexts with the same database name and different <see cref="ICurrentUserService"/> substitutes.</summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options, ICurrentUserService currentUser)
    : ModuleDbContext(options, currentUser), ITestUnitOfWork
{
    protected override string Schema => "test";

    public DbSet<TenantRow> TenantRows => Set<TenantRow>();

    public DbSet<DepartmentRow> DepartmentRows => Set<DepartmentRow>();

    public DbSet<TransferableRow> TransferableRows => Set<TransferableRow>();

    public DbSet<OwnedRow> OwnedRows => Set<OwnedRow>();

    public DbSet<UnscopedRow> UnscopedRows => Set<UnscopedRow>();

    public DbSet<EventfulRow> EventfulRows => Set<EventfulRow>();

    public DbSet<ContactRow> ContactRows => Set<ContactRow>();

    public static string NewDatabaseName() => Guid.NewGuid().ToString("N");

    public static TestDbContext Create(
        string databaseName, ICurrentUserService user, IDateTime? clock = null, params IInterceptor[] extra)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(extra);

        var builder = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName)
            // InMemory has no transactions: BeginTransaction throws unless this warning is ignored, and with it
            // ignored commit and rollback are no-ops (a rollback does not undo writes).
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new AuditingSaveChangesInterceptor(user, clock ?? Clock.Fixed()));

        if (extra.Length > 0)
        {
            builder.AddInterceptors(extra);
        }

        return new TestDbContext(builder.Options, user);
    }

    /// <summary>A context for seeding and for reading back what a test wrote: sees every row, writes anywhere.</summary>
    public static TestDbContext Store(string databaseName) => Create(databaseName, CurrentUsers.Unrestricted());

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ContactRow>().OwnsOne(c => c.Address);

        base.OnModelCreating(modelBuilder);
    }
}
