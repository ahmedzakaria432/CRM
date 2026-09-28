namespace Crm.BuildingBlocks.Application.Abstractions;

/// <summary>
/// The correlation id of whatever is running now: the HTTP request (X-Correlation-Id), or the outbox message
/// the dispatcher is delivering. Every log line, problem body, outbound call, and outbox row carries it.
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>The current request's or dispatched message's correlation id; null outside both.</summary>
    string? CorrelationId { get; }

    /// <summary>Sets the id for the current async flow (outbox dispatcher, background work). Dispose to restore.</summary>
    IDisposable BeginScope(string correlationId);
}
