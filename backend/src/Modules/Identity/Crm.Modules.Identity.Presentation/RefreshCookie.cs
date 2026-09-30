using Microsoft.AspNetCore.Http;

namespace Crm.Modules.Identity.Presentation;

/// <summary>The HttpOnly refresh cookie the browser never reads. Scoped to the Auth endpoints, so it is sent only
/// to login/refresh/logout. API clients and .http files use the JSON body instead.</summary>
internal static class RefreshCookie
{
    public const string Name = "crm_refresh";

    public const string Path = "/api/Agent/Auth";

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
