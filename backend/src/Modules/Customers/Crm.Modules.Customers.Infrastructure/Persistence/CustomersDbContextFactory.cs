using Crm.BuildingBlocks.Infrastructure.Identity;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Customers.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` (backend/scripts/ef.ps1 -Module Customers).</summary>
public sealed class CustomersDbContextFactory : ModuleDesignTimeDbContextFactory<CustomersDbContext>
{
    protected override string Schema => CustomersDbContext.SchemaName;

    protected override CustomersDbContext Create(DbContextOptions<CustomersDbContext> options) => new(options, SystemCurrentUser.Instance);
}
