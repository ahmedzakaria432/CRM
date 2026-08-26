using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Crm.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` resolve CrmDbContext without building and booting Crm.Api. Without this,
/// design-time tooling has to construct the whole host, which breaks as soon as a later story adds
/// startup work that needs a live database.
/// </summary>
public sealed class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    private const string DefaultConnectionString =
        "Server=localhost,14330;Database=Crm;User Id=sa;Password=Crm_Local_Dev_2026!;TrustServerCertificate=True;";

    public CrmDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("CRM_CONNECTION") ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<CrmDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new CrmDbContext(optionsBuilder.Options);
    }
}
