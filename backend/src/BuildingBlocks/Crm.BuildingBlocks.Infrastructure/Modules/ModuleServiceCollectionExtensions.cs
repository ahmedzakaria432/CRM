using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Infrastructure.Modules;

public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// Registers a module's DbContext on the shared <c>Crm</c> connection string, in the module's own
    /// schema, with the auditing interceptor attached; binds the module's unit-of-work interface to it;
    /// and records it so TransactionBehavior and the Development migrator can find it. This is the only
    /// way a module DbContext is registered - never call AddDbContext directly.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext, TUnitOfWork>(
        this IServiceCollection services,
        IConfiguration configuration,
        string moduleName,
        string schema,
        Assembly applicationAssembly)
        where TContext : ModuleDbContext, TUnitOfWork
        where TUnitOfWork : class, IUnitOfWork
    {
        string connectionString = configuration.GetConnectionString("Crm")
            ?? throw new InvalidOperationException("Connection string 'Crm' is not configured.");

        services.AddDbContext<TContext>((sp, options) =>
        {
            options.UseModuleSqlServer(connectionString, schema);
            options.AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>());

            if (sp.GetRequiredService<PersistenceSettings>().EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddScoped<TUnitOfWork>(sp => sp.GetRequiredService<TContext>());
        services.AddSingleton(new ModuleUnitOfWork(moduleName, applicationAssembly, typeof(TUnitOfWork)));
        services.AddSingleton(new ModuleDatabase(moduleName, schema, typeof(TContext)));

        return services;
    }

    /// <summary>Registers the generic repository for one aggregate root of a module.</summary>
    public static IServiceCollection AddRepository<TAggregate, TId, TContext>(this IServiceCollection services)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull
        where TContext : ModuleDbContext
    {
        services.AddScoped<IRepository<TAggregate, TId>, Repository<TAggregate, TId, TContext>>();
        return services;
    }
}
