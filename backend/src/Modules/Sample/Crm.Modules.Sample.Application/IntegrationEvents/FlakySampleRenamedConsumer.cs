// DIAGNOSTICS ONLY - deleted with the Sample module in Story 11.

using System.Collections.Concurrent;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Sample.Contracts.IntegrationEvents;

namespace Crm.Modules.Sample.Application.IntegrationEvents;

/// <summary>
/// Makes the outbox's retry and dead-letter paths reachable by hand (backend/requests/diagnostics.http): a
/// rename to "flaky..." fails the first two deliveries then succeeds; a rename to "poison..." always fails.
/// Any other name is a no-op.
/// </summary>
public sealed class FlakySampleRenamedConsumer : IIntegrationEventConsumer<SampleRenamedIntegrationEvent>
{
    private const int FlakyFailures = 2;
    private static readonly ConcurrentDictionary<Guid, int> Deliveries = new();

    public Task HandleAsync(SampleRenamedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.Name.StartsWith("poison", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Simulated subscriber failure (poison).");
        }

        if (integrationEvent.Name.StartsWith("flaky", StringComparison.OrdinalIgnoreCase)
            && Deliveries.AddOrUpdate(integrationEvent.Id, 1, (_, count) => count + 1) <= FlakyFailures)
        {
            throw new InvalidOperationException("Simulated subscriber failure (flaky).");
        }

        return Task.CompletedTask;
    }
}
