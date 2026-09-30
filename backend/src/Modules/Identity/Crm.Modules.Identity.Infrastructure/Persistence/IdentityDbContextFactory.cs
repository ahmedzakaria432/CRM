using Crm.BuildingBlocks.Infrastructure.Identity;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Identity.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` (backend/scripts/ef.ps1 -Module Identity).</summary>
public sealed class IdentityDbContextFactory : ModuleDesignTimeDbContextFactory<IdentityDbContext>
{
    protected override string Schema => IdentityDbContext.SchemaName;

    protected override IdentityDbContext Create(DbContextOptions<IdentityDbContext> options) =>
        new(options, SystemCurrentUser.Instance);
}
