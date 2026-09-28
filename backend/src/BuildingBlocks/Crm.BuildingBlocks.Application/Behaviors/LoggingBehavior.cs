using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Application.Behaviors;

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
        var stopwatch = Stopwatch.StartNew();

        LogHandling(requestName);

        try
        {
            TResponse response = await next();

            stopwatch.Stop();
            LogHandled(requestName, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            LogFailed(requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestName}")]
    private partial void LogHandling(string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMilliseconds} ms")]
    private partial void LogHandled(string requestName, long elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{RequestName} failed after {ElapsedMilliseconds} ms")]
    private partial void LogFailed(string requestName, long elapsedMilliseconds);
}
