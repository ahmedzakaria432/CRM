using Crm.Modules.Identity.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Modules.Identity.Tests.Support;

public static class JwtSettingsFixture
{
    public const string SigningKey = "0123456789-0123456789-0123456789-abcd";

    public static JwtSettings Valid() =>
        new("crm", "crm-staff", TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(30), SigningKey, "crm-portal");

    public static Dictionary<string, string?> Configuration(string? signingKey = SigningKey) =>
        new()
        {
            ["ConnectionStrings:Crm"] = "Server=localhost;Database=Crm",
            ["Identity:Jwt:Issuer"] = "crm",
            ["Identity:Jwt:Audience"] = "crm-staff",
            ["Identity:Jwt:PortalAudience"] = "crm-portal",
            ["Identity:Jwt:AccessTokenLifetime"] = "00:15:00",
            ["Identity:Jwt:ClockSkew"] = "00:00:30",
            ["Identity:Jwt:SigningKey"] = signingKey,
            ["Identity:Sessions:RefreshTokenLifetime"] = "14.00:00:00",
            ["Identity:Sessions:MaxFailedSignInAttempts"] = "5",
            ["Identity:Sessions:LockoutWindow"] = "00:15:00",
        };
}

public static class Tokens
{
    public static JsonWebToken Read(string jwt) => new JsonWebTokenHandler().ReadJsonWebToken(jwt);

    /// <summary>Validates signature, issuer and audience only: the lifetime check would read the real clock.</summary>
    public static Task<TokenValidationResult> ValidateAsync(string jwt, string audience, string signingKey = JwtSettingsFixture.SigningKey, string issuer = "crm") =>
        new JsonWebTokenHandler().ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
        });
}
