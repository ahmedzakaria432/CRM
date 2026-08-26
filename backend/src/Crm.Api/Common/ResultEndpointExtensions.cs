using Crm.Domain.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Common;

/// <summary>
/// Minimal Result -&gt; HTTP translation. Story 03 replaces this with the global exception handler
/// and a static class of stable RFC 9457 `type` URIs per error class - do not build that out here.
/// </summary>
public static class ResultEndpointExtensions
{
    public static ActionResult ToActionResult(this Error error)
    {
        if (error.Type == ErrorType.Validation)
        {
            return new BadRequestObjectResult(BuildValidationProblemDetails(error));
        }

        return new ObjectResult(new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Description,
            Status = ToStatusCode(error.Type),
        })
        {
            StatusCode = ToStatusCode(error.Type),
        };
    }

    private static ValidationProblemDetails BuildValidationProblemDetails(Error error)
    {
        Dictionary<string, string[]> fieldErrors = error.Description
            .Split(" | ", StringSplitOptions.RemoveEmptyEntries)
            .Select(entry =>
            {
                int separator = entry.IndexOf(':', StringComparison.Ordinal);
                return separator < 0
                    ? (Field: entry, Message: entry)
                    : (Field: entry[..separator].Trim(), Message: entry[(separator + 1)..].Trim());
            })
            .GroupBy(x => x.Field)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());

        return new ValidationProblemDetails(fieldErrors)
        {
            Detail = error.Description,
            Status = StatusCodes.Status400BadRequest,
        };
    }

    // The one shared status-code mapping - Story 03 replaces this with stable `type` URIs per error class.
    private static int ToStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Domain => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };
}
