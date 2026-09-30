using System.Reflection;
using System.Resources;
using System.Text;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Infrastructure.Persistence;
using Crm.Modules.Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Modules.Identity.Infrastructure;

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public Assembly ApplicationAssembly => AssemblyReference.Assembly;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDbContext, IIdentityUnitOfWork>(
            configuration, Name, IdentityDbContext.SchemaName, ApplicationAssembly);

        services.AddRepository<User, UserId, IdentityDbContext>();
        services.AddRepository<RefreshTokenFamily, RefreshTokenFamilyId, IdentityDbContext>();
        services.AddRepository<PortalAccount, PortalAccountId, IdentityDbContext>();
        services.AddRepository<PortalSession, PortalSessionId, IdentityDbContext>();

        services.AddScoped<IModuleSeeder, IdentitySeeder>();

        // The module's public face (Story 15).
        services.AddScoped<IIdentityModuleApi, IdentityModuleApi>();

        // Validation messages that are server text (password rules, unsupported language) follow Accept-Language.
        services.AddLocalizationResource(new ResourceManager(
            "Crm.Modules.Identity.Application.Localization.Resources.IdentityMessages", ApplicationAssembly));

        // A missing or short signing key throws here: the host logs Fatal and exits instead of starting unsigned.
        JwtSettings jwt = configuration.GetSection("Identity:Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException("Identity:Jwt is not configured.");
        jwt.Validate();

        SessionSettings sessions = configuration.GetSection("Identity:Sessions").Get<SessionSettings>()
            ?? throw new InvalidOperationException("Identity:Sessions is not configured.");
        _ = sessions.Lockout; // fails fast on a non-positive limit or window

        services.AddSingleton(jwt);
        services.AddSingleton(sessions);

        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        // Staff and Portal schemes (the host set Staff as the default, Program.cs). The Staff/Portal policies authenticate
        // with both, and the identity's AuthenticationType tells the audiences apart (Story 47, D1).
        services.AddAuthentication()
            .AddJwtBearer(CrmAuthenticationSchemes.Staff, options => ConfigureBearer(options, jwt, jwt.Audience, CrmAuthenticationSchemes.Staff))
            .AddJwtBearer(CrmAuthenticationSchemes.Portal, options => ConfigureBearer(options, jwt, jwt.PortalAudience, CrmAuthenticationSchemes.Portal));
    }

    private static void ConfigureBearer(JwtBearerOptions options, JwtSettings jwt, string audience, string authenticationType)
    {
        options.MapInboundClaims = false; // keep "sub", "role", "membership" as issued
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = jwt.ClockSkew,
            NameClaimType = CrmClaimTypes.UserId,
            RoleClaimType = CrmClaimTypes.Role,
            AuthenticationType = authenticationType,
        };
    }
}
