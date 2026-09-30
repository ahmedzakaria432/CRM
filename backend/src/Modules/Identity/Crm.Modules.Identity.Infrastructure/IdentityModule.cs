using System.Reflection;
using System.Resources;
using System.Text;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Infrastructure.Persistence;
using Crm.Modules.Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
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

        services.AddScoped<IModuleSeeder, IdentitySeeder>();

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

        // The staff scheme. The host set it as the default (Program.cs); Story 47 adds the portal scheme here.
        services.AddAuthentication().AddJwtBearer(CrmAuthenticationSchemes.Staff, options =>
        {
            options.MapInboundClaims = false; // keep "sub", "role", "membership" as issued
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
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
            };
        });
    }
}
