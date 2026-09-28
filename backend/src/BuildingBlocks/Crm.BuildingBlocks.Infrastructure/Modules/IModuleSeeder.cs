namespace Crm.BuildingBlocks.Infrastructure.Modules;

/// <summary>Idempotent Development seed data for one module. Runs after every module's migrations
/// have been applied (see <see cref="ModuleDatabaseExtensions.MigrateAndSeedModulesAsync"/>), so a
/// seeder may rely on its own schema only - never on another module's seed having run first.</summary>
public interface IModuleSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
