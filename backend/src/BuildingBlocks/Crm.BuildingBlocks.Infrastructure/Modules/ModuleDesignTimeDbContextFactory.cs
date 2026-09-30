using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Crm.BuildingBlocks.Infrastructure.Modules;

/// <summary>
/// Lets `dotnet ef` resolve a module's DbContext without building and booting Crm.Api. Without
/// this, design-time tooling has to construct the whole host, which breaks as soon as a later story
/// adds startup work that needs a live database. Each module derives one (e.g. CustomersDbContextFactory).
/// </summary>
public abstract class ModuleDesignTimeDbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    private const string DefaultConnectionString =
        "Server=localhost,14330;Database=Crm;User Id=sa;Password=Crm_Local_Dev_2026!;TrustServerCertificate=True;";

    protected abstract string Schema { get; }

    public TContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("CRM_CONNECTION") ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.UseModuleSqlServer(connectionString, Schema);

        return Create(optionsBuilder.Options);
    }

    protected abstract TContext Create(DbContextOptions<TContext> options);
}
