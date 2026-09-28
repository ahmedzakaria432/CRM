using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Infrastructure.Modules;

public static class ModuleDatabaseExtensions
{
    /// <summary>
    /// Development only: applies every module's pending migrations, each into its own schema, then
    /// runs every module's seeder. Deployment applies migrations as an explicit step per module, not
    /// as a side effect of the process starting.
    /// </summary>
    public static async Task MigrateAndSeedModulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        foreach (ModuleDatabase database in scope.ServiceProvider.GetServices<ModuleDatabase>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(database.DbContextType);
            await context.Database.MigrateAsync(cancellationToken);
        }

        foreach (IModuleSeeder seeder in scope.ServiceProvider.GetServices<IModuleSeeder>())
        {
            await seeder.SeedAsync(cancellationToken);
        }
    }
}
