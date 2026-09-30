using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Presentation.ErrorHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Presentation.Scoping;

/// <summary>Runs after UseAuthentication(), before UseAuthorization(): a forged, stale, or malformed
/// X-Crm-Scope header is rejected here with 403, before any endpoint runs (Story 06). The header can only
/// narrow the caller's scope, never widen it - CurrentUserService already refused to accept anything but a
/// department the caller belongs to.</summary>
public sealed partial class OrganisationScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserService currentUser,
        IProblemDetailsService problems,
        ILocalizationService localization,
        ILogger<OrganisationScopeMiddleware> logger)
    {
        if (!currentUser.Scope.ActiveScopeRejected)
        {
            await next(context);
            return;
        }

        string header = context.Request.Headers[OrganisationScope.HeaderName].ToString();
        LogRejected(logger, header, currentUser.UserIdOrSystem);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Type = ProblemTypes.Forbidden,
            Title = ProblemTypes.TitleFor(ProblemTypes.Forbidden, localization),
            Detail = localization.Get(CrmMessageKeys.ProblemDetail.ScopeRejected),
        };
        problem.Extensions["code"] = "Organisation.ActiveScopeRejected";

        await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected X-Crm-Scope {Header} for user {UserId}")]
    private static partial void LogRejected(ILogger logger, string header, string userId);
}

public static class OrganisationScopeMiddlewareExtensions
{
    public static IApplicationBuilder UseCrmOrganisationScope(this IApplicationBuilder app) =>
        app.UseMiddleware<OrganisationScopeMiddleware>();
}
