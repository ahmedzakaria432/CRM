using Crm.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Presentation.Authorization;

public static class CrmPolicies
{
    /// <summary>A signed-in staff user (Staff JWT scheme). Applied to every Agent and Admin controller by
    /// StaffAreaAuthorizationConvention; what the user may do is decided per request by AuthorizationBehavior.</summary>
    public const string Staff = "Staff";

    public static IServiceCollection AddCrmAuthorization(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(Staff, policy => policy
                .AddAuthenticationSchemes(CrmAuthenticationSchemes.Staff)
                .RequireAuthenticatedUser())
            .Services;
}
