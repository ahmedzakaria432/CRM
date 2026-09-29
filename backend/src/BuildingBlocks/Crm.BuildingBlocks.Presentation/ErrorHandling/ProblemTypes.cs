using Crm.BuildingBlocks.Application.Localization;

namespace Crm.BuildingBlocks.Presentation.ErrorHandling;

/// <summary>
/// The stable RFC 9457 <c>type</c> URI of every error class the API returns. Titles are localised server-side
/// from Accept-Language; the frontend also maps these URNs to its own messages. Never rename one; add a new
/// one instead. URNs rather than https:// URLs: they are stable identifiers, and a URN does not pretend to be
/// dereferenceable. Listed in backend/README.md, "Error responses".
/// </summary>
public static class ProblemTypes
{
    /// <summary>400 - a validator rejected the input.</summary>
    public const string Validation = "urn:crm:problem:validation";

    /// <summary>400 - the request could not be read at all.</summary>
    public const string BadRequest = "urn:crm:problem:bad-request";

    public const string Unauthorized = "urn:crm:problem:unauthorized";

    public const string Forbidden = "urn:crm:problem:forbidden";

    public const string NotFound = "urn:crm:problem:not-found";

    public const string MethodNotAllowed = "urn:crm:problem:method-not-allowed";

    /// <summary>409 - includes the optimistic-concurrency clash (code <c>Persistence.ConcurrencyConflict</c>).</summary>
    public const string Conflict = "urn:crm:problem:conflict";

    public const string UnsupportedMediaType = "urn:crm:problem:unsupported-media-type";

    /// <summary>422 - an aggregate invariant refused the change.</summary>
    public const string DomainRule = "urn:crm:problem:domain-rule";

    public const string RateLimited = "urn:crm:problem:rate-limited";

    /// <summary>500 - never carries the exception's message.</summary>
    public const string Internal = "urn:crm:problem:internal";

    /// <summary>503 - an outbound call failed, timed out, or its circuit is open.</summary>
    public const string DependencyUnavailable = "urn:crm:problem:dependency-unavailable";

    public static string ForStatus(int statusCode) => statusCode switch
    {
        400 => BadRequest,
        401 => Unauthorized,
        403 => Forbidden,
        404 => NotFound,
        405 => MethodNotAllowed,
        409 => Conflict,
        415 => UnsupportedMediaType,
        422 => DomainRule,
        429 => RateLimited,
        503 => DependencyUnavailable,
        >= 500 => Internal,
        _ => BadRequest,
    };

    /// <summary>The problem title in the request's language (Accept-Language). Resource key
    /// "Problem.Title.&lt;urn suffix&gt;"; an unknown type gets the internal-error title.</summary>
    public static string TitleFor(string type, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        string known = type switch
        {
            Validation or BadRequest or Unauthorized or Forbidden or NotFound or MethodNotAllowed or Conflict
                or UnsupportedMediaType or DomainRule or RateLimited or DependencyUnavailable => type,
            _ => Internal,
        };
        return localization.Get("Problem.Title." + known["urn:crm:problem:".Length..]);
    }
}
