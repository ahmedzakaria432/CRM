using Crm.Domain.Common.Results;

namespace Crm.Api.Common;

/// <summary>
/// Minimal Result -&gt; HTTP translation. Story 03 replaces this with the global exception handler
/// and a static class of stable RFC 9457 `type` URIs per error class - do not build that out here.
/// </summary>
public static class ResultEndpointExtensions
{
    public static IResult ToProblem(this Error error)
    {
        if (error.Type == ErrorType.Validation)
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

            return Results.ValidationProblem(fieldErrors, detail: error.Description, statusCode: 400);
        }

        int statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Domain => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(detail: error.Description, statusCode: statusCode, title: error.Code);
    }
}
