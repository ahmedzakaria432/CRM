using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Crm.BuildingBlocks.Infrastructure.Correlation;

/// <summary>
/// Accepts X-Correlation-Id, generates one when it is absent or unsafe, stores it for the request, and returns
/// it on the response. Must be the first middleware (Program.cs) so every later log line and problem sees it.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "Crm.CorrelationId";
    private const int MaxLength = 64;

    public Task InvokeAsync(HttpContext context)
    {
        string correlationId = IsAcceptable(context.Request.Headers[HeaderName].ToString())
            ? context.Request.Headers[HeaderName].ToString()
            // A generated id equals the trace id, so logs and traces join on one value.
            : Activity.Current?.TraceId.ToHexString() ?? Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        Activity.Current?.SetTag("crm.correlation_id", correlationId);

        // OnStarting, not a direct header write: ExceptionHandlerMiddleware clears response headers before
        // writing the 500, and a header set up front would be lost on exactly the response that needs it most.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        return next(context);
    }

    // Only a short token of safe characters is echoed. Anything else (CR/LF, spaces, a 10 KB header) is a log-
    // or header-injection vector and is replaced, never reflected.
    private static bool IsAcceptable(string value) =>
        value.Length is > 0 and <= MaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or ':' or '-');
}
