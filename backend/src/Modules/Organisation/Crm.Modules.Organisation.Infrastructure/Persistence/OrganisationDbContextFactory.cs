using Crm.BuildingBlocks.Infrastructure.Identity;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Organisation.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` (backend/scripts/ef.ps1 -Module Organisation).</summary>
public sealed class OrganisationDbContextFactory : ModuleDesignTimeDbContextFactory<OrganisationDbContext>
{
    protected override string Schema => OrganisationDbContext.SchemaName;

    protected override OrganisationDbContext Create(DbContextOptions<OrganisationDbContext> options) =>
        new(options, SystemCurrentUser.Instance);
}
