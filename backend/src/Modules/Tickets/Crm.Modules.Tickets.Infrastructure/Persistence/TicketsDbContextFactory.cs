using Crm.BuildingBlocks.Infrastructure.Identity;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Tickets.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` (backend/scripts/ef.ps1 -Module Tickets).</summary>
public sealed class TicketsDbContextFactory : ModuleDesignTimeDbContextFactory<TicketsDbContext>
{
    protected override string Schema => TicketsDbContext.SchemaName;

    protected override TicketsDbContext Create(DbContextOptions<TicketsDbContext> options) => new(options, SystemCurrentUser.Instance);
}
