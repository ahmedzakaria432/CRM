using System.Text;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Modules.Identity.Infrastructure.Security;

/// <summary>Issues the staff access token: HS256, exactly the claim shape Story 06's dev handler built (sub,
/// tenant_id, one membership per membership, one role per distinct role) plus name and sid. System.DateTime is
/// banned, so exp/iat/nbf go in as Unix seconds through the claims dictionary and the handler's default
/// time stamping is off.</summary>
public sealed class JwtAccessTokenIssuer(JwtSettings settings) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256);

    public IssuedAccessToken Issue(AccessTokenSubject subject, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(subject);

        DateTimeOffset expires = nowUtc + settings.AccessTokenLifetime;

        var claims = new Dictionary<string, object>
        {
            [CrmClaimTypes.UserId] = subject.UserId.ToString("D"),
            [CrmClaimTypes.DisplayName] = subject.DisplayName,
            [CrmClaimTypes.TenantId] = subject.TenantId.ToString("D"),
            [CrmClaimTypes.SessionId] = subject.SessionId.ToString("D"),
            [CrmClaimTypes.Membership] = subject.Memberships.Select(m => $"{m.DepartmentId:D}|{m.BranchId:D}|{m.Role}").ToArray(),
            [CrmClaimTypes.Role] = subject.Memberships.Select(m => m.Role).Distinct(StringComparer.Ordinal).ToArray(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            [JwtRegisteredClaimNames.Iat] = nowUtc.ToUnixTimeSeconds(),
            [JwtRegisteredClaimNames.Nbf] = nowUtc.ToUnixTimeSeconds(),
            [JwtRegisteredClaimNames.Exp] = expires.ToUnixTimeSeconds(),
        };

        string token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Claims = claims,
            SigningCredentials = _credentials,
        });

        return new IssuedAccessToken(token, expires);
    }
}
