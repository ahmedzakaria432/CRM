using Crm.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Crm.BuildingBlocks.Infrastructure.Correlation;

/// <summary>
/// Singleton. Reads an AsyncLocal override first (set by <see cref="BeginScope"/> in background work), then
/// the id CorrelationIdMiddleware stored in HttpContext.Items. Both are needed: an AsyncLocal set inside a
/// middleware is NOT visible to middleware outside it once the call unwinds (the exception handler would lose
/// it), and IHttpClientFactory handlers run in their own DI scope, so a scoped accessor would be invisible to
/// the outbound correlation handler.
/// </summary>
public sealed class CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> Override = new();

    public string? CorrelationId =>
        Override.Value
        ?? httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string;

    public IDisposable BeginScope(string correlationId)
    {
        string? previous = Override.Value;
        Override.Value = correlationId;
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        public void Dispose() => Override.Value = previous;
    }
}
