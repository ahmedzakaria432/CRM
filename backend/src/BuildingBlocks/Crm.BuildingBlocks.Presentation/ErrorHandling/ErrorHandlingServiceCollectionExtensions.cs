using Crm.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Presentation.ErrorHandling;

public static class ErrorHandlingServiceCollectionExtensions
{
    /// <summary>
    /// The global exception handler plus the one ProblemDetails customisation every problem passes through:
    /// exception-handler output, status-code pages, [ApiController] model-state 400s, and ApiControllerBase's
    /// Result mapping (ProblemDetailsFactory applies it - verified by probe while planning Story 03).
    /// </summary>
    public static IServiceCollection AddCrmErrorHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            ProblemDetails problem = context.ProblemDetails;
            int status = problem.Status ?? context.HttpContext.Response.StatusCode;

            // Framework-produced problems carry the RFC 9110 link or nothing. Replace it with ours; never
            // overwrite a type our own code already chose.
            if (problem.Type is null || problem.Type.StartsWith("https://tools.ietf.org/", StringComparison.Ordinal))
            {
                problem.Type = problem is ValidationProblemDetails ? ProblemTypes.Validation : ProblemTypes.ForStatus(status);
                problem.Title = ProblemTypes.TitleFor(problem.Type);
            }

            problem.Instance ??= context.HttpContext.Request.Path;

            string? correlationId = context.HttpContext.RequestServices
                .GetRequiredService<ICorrelationIdAccessor>().CorrelationId;

            if (correlationId is not null)
            {
                problem.Extensions["correlationId"] = correlationId;
            }

            // traceId is added by the framework - not added again here.
        });

        return services;
    }
}
