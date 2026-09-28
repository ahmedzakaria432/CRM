using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Identity;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Crm.BuildingBlocks.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Crm.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Everything every module shares: the clock, the current user, domain-event
    /// dispatch, the auditing interceptor, and the SQL Server readiness check. Module-owned
    /// services are registered by each module's <see cref="Modules.IModule.AddServices"/>.</summary>
    public static IServiceCollection AddBuildingBlocksInfrastructure(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHealthChecks()
            .AddSqlServer(
                connectionString: configuration.GetConnectionString("Crm")
                    ?? throw new InvalidOperationException("Connection string 'Crm' is not configured."),
                name: "sql-server",
                tags: ["ready"]);

        services.AddSingleton<IDateTime, SystemDateTime>();
        services.AddSingleton(new PersistenceSettings(EnableSensitiveDataLogging: environment.IsDevelopment()));
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<AuditingSaveChangesInterceptor>();

        return services;
    }
}
