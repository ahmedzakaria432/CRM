using System.Diagnostics;
using Crm.BuildingBlocks.Application.Diagnostics;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Application.Behaviors;

/// <summary>
/// The observability stage of the pipeline: a log line, an OpenTelemetry span, and a duration metric per
/// request, so a slow handler is attributable to a request without a debugger.
/// </summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;
        string requestKind = request is ICommandMarker ? "command" : "query";
        var stopwatch = Stopwatch.StartNew();

        using Activity? activity = CrmDiagnostics.ActivitySource.StartActivity(requestName);
        activity?.SetTag("crm.request.name", requestName);
        activity?.SetTag("crm.request.kind", requestKind);

        LogHandling(requestName);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            // Goes through the redaction policy - secret-named properties render as "***".
            LogRequestPayload(requestName, request);
        }

        try
        {
            TResponse response = await next();

            stopwatch.Stop();

            if (response is Result { IsFailure: true } failure)
            {
                activity?.SetStatus(ActivityStatusCode.Error, failure.Error.Code);
                activity?.SetTag("crm.error.code", failure.Error.Code);
                Record(requestName, requestKind, "failure", stopwatch.Elapsed);
                LogHandledWithError(requestName, failure.Error.Code, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                Record(requestName, requestKind, "success", stopwatch.Elapsed);
                LogHandled(requestName, stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            Record(requestName, requestKind, "exception", stopwatch.Elapsed);
            LogFailed(requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private static void Record(string requestName, string requestKind, string outcome, TimeSpan elapsed) =>
        CrmDiagnostics.RequestDuration.Record(
            elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("crm.request.name", requestName),
            new KeyValuePair<string, object?>("crm.request.kind", requestKind),
            new KeyValuePair<string, object?>("crm.outcome", outcome));

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestName}")]
    private partial void LogHandling(string requestName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling {RequestName} {@Request}")]
    private partial void LogRequestPayload(string requestName, TRequest request);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMilliseconds} ms")]
    private partial void LogHandled(string requestName, long elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handled {RequestName} with {ErrorCode} in {ElapsedMilliseconds} ms")]
    private partial void LogHandledWithError(string requestName, string errorCode, long elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{RequestName} failed after {ElapsedMilliseconds} ms")]
    private partial void LogFailed(string requestName, long elapsedMilliseconds);
}
