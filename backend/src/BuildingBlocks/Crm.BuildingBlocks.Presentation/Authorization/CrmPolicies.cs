using Crm.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Presentation.Authorization;

public static class CrmPolicies
{
    /// <summary>Authenticated, with the Administrator role in at least one department. Story 07/09 refine it.</summary>
    public const string Administrator = "Administrator";

    public static IServiceCollection AddCrmAuthorization(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(Administrator, policy => policy.RequireAuthenticatedUser().RequireRole(CrmRoles.Administrator))
            .Services;
}
