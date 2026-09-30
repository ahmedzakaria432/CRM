namespace Crm.BuildingBlocks.Application.Localization;

/// <summary>Every resource key in <c>Resources/CrmMessages*.resx</c>. Callers pass these constants to
/// <see cref="ILocalizationService.Get"/>; a key is never written as a string literal at a call site.</summary>
public static class CrmMessageKeys
{
    /// <summary>Problem titles, one per <c>urn:crm:problem:*</c> type.</summary>
    public static class ProblemTitle
    {
        public const string Validation = "Problem.Title.validation";
        public const string BadRequest = "Problem.Title.bad-request";
        public const string Unauthorized = "Problem.Title.unauthorized";
        public const string Forbidden = "Problem.Title.forbidden";
        public const string NotFound = "Problem.Title.not-found";
        public const string MethodNotAllowed = "Problem.Title.method-not-allowed";
        public const string Conflict = "Problem.Title.conflict";
        public const string UnsupportedMediaType = "Problem.Title.unsupported-media-type";
        public const string DomainRule = "Problem.Title.domain-rule";
        public const string RateLimited = "Problem.Title.rate-limited";
        public const string Internal = "Problem.Title.internal";
        public const string DependencyUnavailable = "Problem.Title.dependency-unavailable";
    }

    /// <summary>Fixed problem details the API writes itself.</summary>
    public static class ProblemDetail
    {
        public const string Validation = "Problem.Detail.Validation";
        public const string Concurrency = "Problem.Detail.Concurrency";
        public const string Unreadable = "Problem.Detail.Unreadable";
        public const string DependencyUnavailable = "Problem.Detail.DependencyUnavailable";
        public const string Unexpected = "Problem.Detail.Unexpected";
        public const string RateLimited = "Problem.Detail.RateLimited";
        public const string ScopeRejected = "Problem.Detail.ScopeRejected";
        public const string OutOfScope = "Problem.Detail.OutOfScope";
    }
}
