using Crm.SharedKernel.Results;
using Microsoft.AspNetCore.Http;

namespace Crm.BuildingBlocks.Presentation.ErrorHandling;

/// <summary>The one ErrorType -> HTTP mapping. Every Result-to-problem translation goes through here.</summary>
public static class ErrorTypeMapping
{
    public static int ToStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Domain => StatusCodes.Status422UnprocessableEntity,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static string ToProblemType(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => ProblemTypes.Validation,
        ErrorType.NotFound => ProblemTypes.NotFound,
        ErrorType.Conflict => ProblemTypes.Conflict,
        ErrorType.Forbidden => ProblemTypes.Forbidden,
        ErrorType.Domain => ProblemTypes.DomainRule,
        ErrorType.Unauthorized => ProblemTypes.Unauthorized,
        _ => ProblemTypes.Internal,
    };
}
