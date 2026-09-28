using Crm.BuildingBlocks.Application.Exceptions;
using Crm.SharedKernel;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Polly;

namespace Crm.BuildingBlocks.Presentation.ErrorHandling;

/// <summary>
/// Turns every exception that reaches the pipeline into an RFC 9457 problem with a <see cref="ProblemTypes"/>
/// type. A 500 NEVER carries the exception's message - that can hold a connection string or internal detail.
/// Every branch logs: .NET 10 does not log an exception an IExceptionHandler handled (verified by probe while
/// planning Story 03), so a branch that skipped logging would make the failure invisible.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away. The framework treats this as an abort; there is nobody to answer.
            return false;
        }

        ProblemDetails problem = exception switch
        {
            DomainException domain => DomainRule(domain),
            ValidationException validation => ValidationFailed(validation),
            ConcurrencyConflictException concurrency => ConcurrencyConflict(concurrency),
            BadHttpRequestException badRequest => Unreadable(badRequest),
            ExecutionRejectedException or HttpRequestException => DependencyUnavailable(exception),
            _ => Unexpected(exception),
        };

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private ProblemDetails DomainRule(DomainException exception)
    {
        LogDomainRule(exception.Error.Code);
        return Create(StatusCodes.Status422UnprocessableEntity, ProblemTypes.DomainRule, exception.Error.Message, exception.Error.Code);
    }

    private ValidationProblemDetails ValidationFailed(ValidationException exception)
    {
        LogValidation();
        Dictionary<string, string[]> errors = exception.Errors
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Type = ProblemTypes.Validation,
            Title = ProblemTypes.TitleFor(ProblemTypes.Validation),
            Detail = "One or more validation errors occurred.",
        };
    }

    private ProblemDetails ConcurrencyConflict(ConcurrencyConflictException exception)
    {
        LogConcurrency(exception);
        return Create(
            StatusCodes.Status409Conflict,
            ProblemTypes.Conflict,
            "The record was modified by someone else. Reload and try again.",
            "Persistence.ConcurrencyConflict");
    }

    private ProblemDetails Unreadable(BadHttpRequestException exception)
    {
        LogUnreadable(exception.StatusCode);
        return Create(exception.StatusCode, ProblemTypes.ForStatus(exception.StatusCode), "The request could not be read.", code: null);
    }

    private ProblemDetails DependencyUnavailable(Exception exception)
    {
        LogDependencyUnavailable(exception);
        return Create(
            StatusCodes.Status503ServiceUnavailable,
            ProblemTypes.DependencyUnavailable,
            "A downstream service is unavailable. Try again shortly.",
            code: null);
    }

    private ProblemDetails Unexpected(Exception exception)
    {
        LogUnexpected(exception);
        return Create(StatusCodes.Status500InternalServerError, ProblemTypes.Internal, "An unexpected error occurred.", code: null);
    }

    private static ProblemDetails Create(int status, string type, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Type = type,
            Title = ProblemTypes.TitleFor(type),
            Detail = detail,
        };

        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        return problem;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Domain rule {ErrorCode} refused the request")]
    private partial void LogDomainRule(string errorCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Validation exception reached the pipeline")]
    private partial void LogValidation();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Concurrency conflict reached the pipeline")]
    private partial void LogConcurrency(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unreadable request, status {StatusCode}")]
    private partial void LogUnreadable(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbound dependency unavailable")]
    private partial void LogDependencyUnavailable(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private partial void LogUnexpected(Exception exception);
}
