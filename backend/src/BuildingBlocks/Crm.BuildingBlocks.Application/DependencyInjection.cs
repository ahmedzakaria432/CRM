using System.Reflection;
using Crm.BuildingBlocks.Application.Behaviors;
using Crm.BuildingBlocks.Application.Modules;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR and FluentValidation ONCE, over every module's Application assembly. Called
    /// by the host only - a module must never call AddMediatR itself, because each call would register
    /// the pipeline behaviours again and run every request through them twice.
    /// </summary>
    public static IServiceCollection AddBuildingBlocksApplication(
        this IServiceCollection services, IReadOnlyCollection<Assembly> moduleApplicationAssemblies)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies([.. moduleApplicationAssemblies]);

            // Order is the execution order (CLAUDE.md, "Backend"):
            // validation -> authorisation -> logging -> transaction -> caching.
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        services.AddValidatorsFromAssemblies(moduleApplicationAssemblies);
        services.AddScoped<IUnitOfWorkResolver, UnitOfWorkResolver>();

        return services;
    }
}
