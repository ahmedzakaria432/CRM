using System.Reflection;
using System.Resources;
using Crm.BuildingBlocks.Application.Behaviors;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Application.Modules;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR, FluentValidation, and integration-event handlers ONCE, over every module's
    /// Application assembly. Called by the host only - a module must never call AddMediatR itself, because
    /// each call would register the pipeline behaviours again and run every request through them twice.
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
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
        });

        services.AddValidatorsFromAssemblies(moduleApplicationAssemblies);
        services.AddScoped<IUnitOfWorkResolver, UnitOfWorkResolver>();

        // Story 05: server-side text follows Accept-Language. FluentValidation's built-in messages localise
        // themselves from CurrentUICulture (Probe 2); these resources cover problem titles and fixed details.
        services.AddLocalizationResource(new ResourceManager(
            "Crm.BuildingBlocks.Application.Localization.Resources.CrmMessages", typeof(DependencyInjection).Assembly));
        services.AddSingleton<ILocalizationService, ResourceLocalizationService>();

        // Integration events (Story 03): one bus per scope; handlers discovered here, registered by their
        // concrete type so the outbox dispatcher can resolve each one in its own scope and transaction.
        services.AddScoped<OutboxEventBus>();
        services.AddScoped<IEventBus>(sp => sp.GetRequiredService<OutboxEventBus>());
        services.AddScoped<IPendingIntegrationEvents>(sp => sp.GetRequiredService<OutboxEventBus>());

        var registry = IntegrationEventConsumerRegistry.Scan(moduleApplicationAssemblies);
        services.AddSingleton(registry);
        foreach (Type handlerType in registry.HandlerTypes)
        {
            services.AddScoped(handlerType);
        }

        return services;
    }
}
