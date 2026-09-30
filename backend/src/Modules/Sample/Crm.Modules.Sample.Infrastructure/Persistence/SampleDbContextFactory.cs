// CANONICAL SLICE - deleted alongside the rest of the Sample module in Story 11.

using Crm.BuildingBlocks.Infrastructure.Identity;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Sample.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` (backend/scripts/ef.ps1 -Module Sample).</summary>
public sealed class SampleDbContextFactory : ModuleDesignTimeDbContextFactory<SampleDbContext>
{
    protected override string Schema => SampleDbContext.SchemaName;

    protected override SampleDbContext Create(DbContextOptions<SampleDbContext> options) => new(options, SystemCurrentUser.Instance);
}
