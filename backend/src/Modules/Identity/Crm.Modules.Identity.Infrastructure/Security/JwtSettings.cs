using System.Text;

namespace Crm.Modules.Identity.Infrastructure.Security;

/// <summary>Bound from "Identity:Jwt". The signing key is never in appsettings.json - environment or secret
/// store only (Identity__Jwt__SigningKey); the app refuses to start without it.</summary>
public sealed record JwtSettings(string Issuer, string Audience, TimeSpan AccessTokenLifetime, TimeSpan ClockSkew, string SigningKey = "", string PortalAudience = "")
{
    private const int MinimumKeyBytes = 32;

    internal void Validate()
    {
        Require(!string.IsNullOrWhiteSpace(Issuer), "Issuer");
        Require(!string.IsNullOrWhiteSpace(Audience), "Audience");
        // The two audiences must differ: the staff/portal separation (Story 47, D1) depends on it.
        Require(!string.IsNullOrWhiteSpace(PortalAudience) && !string.Equals(PortalAudience, Audience, StringComparison.Ordinal), "PortalAudience");
        Require(!string.IsNullOrEmpty(SigningKey) && Encoding.UTF8.GetByteCount(SigningKey) >= MinimumKeyBytes, "SigningKey");
        Require(AccessTokenLifetime > TimeSpan.Zero, "AccessTokenLifetime");
        Require(ClockSkew >= TimeSpan.Zero, "ClockSkew");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                $"Identity:Jwt:{name} is not configured. Set it through the environment (Identity__Jwt__{name}), never appsettings.json. "
                + "The signing key must be at least 32 bytes.");
        }
    }
}
