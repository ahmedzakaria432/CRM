using System.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Application.Localization;

/// <summary>A resource bundle registered for lookup by <see cref="ResourceLocalizationService"/>. Lookup order
/// is registration order; the building blocks register first.</summary>
public sealed record LocalizationResource(ResourceManager Manager);

public static class LocalizationServiceCollectionExtensions
{
    /// <summary>A module that needs its own messages (from Story 11 on) adds a .resx pair to its own
    /// Application project and calls this from its <c>IModule.AddServices</c>. It never adds keys to the
    /// building-block resource.</summary>
    public static IServiceCollection AddLocalizationResource(this IServiceCollection services, ResourceManager manager)
    {
        services.AddSingleton(new LocalizationResource(manager));
        return services;
    }
}
