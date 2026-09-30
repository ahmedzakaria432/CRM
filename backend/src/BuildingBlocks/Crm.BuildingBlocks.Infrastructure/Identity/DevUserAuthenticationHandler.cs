using System.Security.Claims;
using System.Text.Encodings.Web;
using Crm.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.BuildingBlocks.Infrastructure.Identity;

/// <summary>DEVELOPMENT ONLY (Story 06): accepts an X-Dev-User header naming one of the seeded well-known user
/// ids and signs the caller in from that user's memberships. Registered in every environment (Program.cs needs
/// a default scheme or an [Authorize] challenge becomes a 500), but returns NoResult outside Development before
/// reading the header at all - so a forged header in Production is simply ignored, never a 500. Deleted by
/// Story 07, which replaces it with JWT bearer.</summary>
public static class DevUserAuthentication
{
    public const string Scheme = "DevUser";
    public const string HeaderName = "X-Dev-User";
}

public sealed partial class DevUserAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment())
        {
            return AuthenticateResult.NoResult();
        }

        string? header = Request.Headers[DevUserAuthentication.HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header))
        {
            return AuthenticateResult.NoResult();
        }

        if (!Guid.TryParse(header, out Guid userId))
        {
            LogRejected(header, "not a Guid");
            return AuthenticateResult.Fail("Invalid X-Dev-User header.");
        }

        var directory = Context.RequestServices.GetRequiredService<IDevUserDirectory>();
        DevUser? devUser = await directory.FindAsync(userId, Context.RequestAborted);

        if (devUser is null)
        {
            LogRejected(header, "unknown user");
            return AuthenticateResult.Fail("Unknown X-Dev-User.");
        }

        List<Claim> claims =
        [
            new Claim(CrmClaimTypes.UserId, devUser.UserId.ToString()),
            new Claim(CrmClaimTypes.TenantId, devUser.TenantId.ToString()),
        ];

        claims.AddRange(devUser.Memberships.Select(m =>
            new Claim(CrmClaimTypes.Membership, $"{m.DepartmentId:D}|{m.BranchId:D}|{m.Role}")));

        claims.AddRange(devUser.Memberships.Select(m => m.Role).Distinct(StringComparer.Ordinal)
            .Select(role => new Claim(CrmClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, DevUserAuthentication.Scheme, CrmClaimTypes.UserId, CrmClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, DevUserAuthentication.Scheme);

        return AuthenticateResult.Success(ticket);
    }

    // The raw header value is not sensitive - it is a user id, never a credential.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Dev login rejected for X-Dev-User {DevUser}: {Reason}")]
    private partial void LogRejected(string devUser, string reason);
}

public static class DevUserAuthenticationExtensions
{
    public static AuthenticationBuilder AddCrmDevUser(this AuthenticationBuilder builder) =>
        builder.AddScheme<AuthenticationSchemeOptions, DevUserAuthenticationHandler>(DevUserAuthentication.Scheme, null);
}
