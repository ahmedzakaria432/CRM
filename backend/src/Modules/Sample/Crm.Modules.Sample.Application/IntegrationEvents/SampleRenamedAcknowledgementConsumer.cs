// CANONICAL SLICE - the shape of an integration-event subscriber.
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Sample.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Sample.Application.IntegrationEvents;

/// <summary>
/// In a real module the subscriber lives in ANOTHER module's Application project, referencing the publisher's
/// Contracts. Sample is the only module, so it subscribes to itself. Discovered automatically; runs in this
/// module's own transaction, once per event (the dispatcher's idempotency record).
/// </summary>
public sealed partial class SampleRenamedAcknowledgementConsumer(ILogger<SampleRenamedAcknowledgementConsumer> logger)
    : IIntegrationEventConsumer<SampleRenamedIntegrationEvent>
{
    public Task HandleAsync(SampleRenamedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        LogAcknowledged(integrationEvent.SampleId, integrationEvent.Name);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample {SampleId} renamed to {SampleName} - acknowledged")]
    private partial void LogAcknowledged(Guid sampleId, string sampleName);
}
