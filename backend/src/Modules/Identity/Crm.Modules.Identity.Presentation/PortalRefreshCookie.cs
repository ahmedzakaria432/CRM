using Microsoft.AspNetCore.Http;

namespace Crm.Modules.Identity.Presentation;

/// <summary>The HttpOnly portal refresh cookie the browser never reads (Story 47). Scoped to the portal Auth endpoints,
/// so it is sent only to register/login/refresh/logout and never to the staff ones (a different name and path).</summary>
internal static class PortalRefreshCookie
{
    public const string Name = "crm_portal_refresh";

    public const string Path = "/api/Portal/Auth";

    public static CookieOptions Options(DateTimeOffset expiresAtUtc, bool secure) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAtUtc,
        IsEssential = true,
    };

    public static CookieOptions DeleteOptions(bool secure) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        IsEssential = true,
    };
}
