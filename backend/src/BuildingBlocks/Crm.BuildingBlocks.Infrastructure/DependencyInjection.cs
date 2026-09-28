using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Infrastructure.Caching;
using Crm.BuildingBlocks.Infrastructure.Correlation;
using Crm.BuildingBlocks.Infrastructure.Identity;
using Crm.BuildingBlocks.Infrastructure.Outbox;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Crm.BuildingBlocks.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Crm.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Everything every module shares: the clock, the current user, domain-event dispatch, the
    /// auditing interceptor, the SQL Server readiness check, the distributed cache, the outbound correlation
    /// handler, and the outbox dispatcher. Module-owned services are registered by each module's
    /// <see cref="Modules.IModule.AddServices"/>.</summary>
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
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<AuditingSaveChangesInterceptor>();

        AddCaching(services, configuration);

        services.AddTransient<CorrelationIdDelegatingHandler>();

        services.AddSingleton(configuration.GetSection("Outbox").Get<OutboxSettings>() ?? OutboxSettings.Default);
        services.AddHostedService<OutboxDispatcher>();

        return services;
    }

    /// <summary>True when no Redis connection string is configured and the in-process cache is used. The
    /// host logs a startup warning: that fallback is correct on ONE instance only.</summary>
    public static bool UsesInProcessCache(IConfiguration configuration) =>
        configuration.GetConnectionString("Redis") is null;

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        string? redis = configuration.GetConnectionString("Redis");
        if (redis is not null)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redis;
                options.InstanceName = "crm:";
            });
        }
        else
        {
            // Single-instance fallback. Invalidation is local to this process - never run more than one
            // instance like this.
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton(configuration.GetSection("Caching").Get<CachingSettings>() ?? CachingSettings.Default);
        services.AddSingleton<ICacheService, DistributedCacheService>();
    }
}
