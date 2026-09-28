using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Crm.BuildingBlocks.Infrastructure.Modules;

public static class ModuleSqlServer
{
    /// <summary>
    /// The one SQL Server configuration every module DbContext uses, at runtime and at design time.
    /// The migrations-history table lives in the module's own schema: with one shared
    /// <c>dbo.__EFMigrationsHistory</c>, every module's migrator would see the other modules'
    /// migrations as unknown rows and the modules could not be migrated independently.
    /// </summary>
    public static DbContextOptionsBuilder UseModuleSqlServer(
        this DbContextOptionsBuilder options, string connectionString, string schema) =>
        options.UseSqlServer(
            connectionString,
            sql => sql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema));
}
