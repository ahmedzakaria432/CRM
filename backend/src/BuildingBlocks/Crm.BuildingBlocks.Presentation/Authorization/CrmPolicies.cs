using System.Security.Claims;
using Crm.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Presentation.Authorization;

public static class CrmPolicies
{
    /// <summary>A signed-in staff user (Staff JWT audience). Applied to every Agent and Admin controller by
    /// StaffAreaAuthorizationConvention; what the user may do is decided per request by AuthorizationBehavior.</summary>
    public const string Staff = "Staff";

    /// <summary>A signed-in portal customer (Portal JWT audience). Applied to every Portal controller by
    /// PortalAreaAuthorizationConvention (Story 47).</summary>
    public const string Portal = "Portal";

    public static IServiceCollection AddCrmAuthorization(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            // Both schemes authenticate, so a token of the OTHER audience is authenticated-but-forbidden (403, Story 47 D1).
            .AddPolicy(Staff, policy => policy
                .AddAuthenticationSchemes(CrmAuthenticationSchemes.Staff, CrmAuthenticationSchemes.Portal)
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasIdentity(context.User, CrmAuthenticationSchemes.Staff)))
            .AddPolicy(Portal, policy => policy
                .AddAuthenticationSchemes(CrmAuthenticationSchemes.Staff, CrmAuthenticationSchemes.Portal)
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasIdentity(context.User, CrmAuthenticationSchemes.Portal)))
            .Services;

    private static bool HasIdentity(ClaimsPrincipal user, string scheme) =>
        user.Identities.Any(i => i.IsAuthenticated && string.Equals(i.AuthenticationType, scheme, StringComparison.Ordinal));
}
