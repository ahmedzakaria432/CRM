using System.Text.Json;
using Crm.SharedKernel;

namespace Crm.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// One integration event waiting for delivery, in the PUBLISHING module's schema (<c>{schema}.outbox_messages</c>).
/// Written in the same transaction as the change that raised it; delivered after commit by OutboxDispatcher.
/// Dead-lettered when <see cref="Attempts"/> reaches the configured maximum: ProcessedAtUtc stays null and
/// NextAttemptAtUtc becomes null. Never deleted automatically.
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxErrorLength = 2000;

    public Guid Id { get; private set; }

    /// <summary>The event type's FullName - the key IntegrationEventConsumerRegistry resolves.</summary>
    public string Type { get; private set; }

    public string Content { get; private set; }

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public string? CorrelationId { get; private set; }

    /// <summary>The W3C traceparent of the publishing request, so dispatch spans join its trace.</summary>
    public string? TraceParent { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    private OutboxMessage()
    {
        Type = string.Empty;
        Content = string.Empty;
    }

    public static OutboxMessage From(IIntegrationEvent integrationEvent, string? correlationId, string? traceParent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        Type eventType = integrationEvent.GetType();

        return new OutboxMessage
        {
            Id = integrationEvent.Id,
            Type = eventType.FullName ?? eventType.Name,
            Content = JsonSerializer.Serialize(integrationEvent, eventType, OutboxJson.Options),
            OccurredOnUtc = integrationEvent.OccurredOnUtc,
            CorrelationId = correlationId,
            TraceParent = traceParent,
        };
    }

    public void MarkProcessed(DateTimeOffset nowUtc)
    {
        ProcessedAtUtc = nowUtc;
        NextAttemptAtUtc = null;
    }

    /// <summary>Records a failed delivery and schedules the next one: 2, 4, 8, 16... seconds, capped at 5
    /// minutes. Returns true when the message is now dead-lettered.</summary>
    public bool RecordFailure(string error, DateTimeOffset nowUtc, int maxAttempts)
    {
        ArgumentNullException.ThrowIfNull(error);

        Attempts++;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;

        if (Attempts >= maxAttempts)
        {
            NextAttemptAtUtc = null;
            return true;
        }

        NextAttemptAtUtc = nowUtc.AddSeconds(Math.Min(Math.Pow(2, Attempts), 300));
        return false;
    }
}

/// <summary>The idempotency record: this handler has applied this message. Written in the handler's own
/// transaction, in the SUBSCRIBING module's schema (<c>{schema}.outbox_message_consumers</c>).</summary>
public sealed class OutboxMessageConsumer
{
    public Guid MessageId { get; private set; }

    public string Handler { get; private set; }

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    private OutboxMessageConsumer()
    {
        Handler = string.Empty;
    }

    public OutboxMessageConsumer(Guid messageId, string handler, DateTimeOffset processedAtUtc)
    {
        MessageId = messageId;
        Handler = handler;
        ProcessedAtUtc = processedAtUtc;
    }
}

internal static class OutboxJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
