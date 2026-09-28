// CANONICAL SLICE - the shape of an integration event: a record in the PUBLISHING module's Contracts.
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.SharedKernel;

namespace Crm.Modules.Sample.Contracts.IntegrationEvents;

/// <summary>Published by RenameSampleCommandHandler through IEventBus. Primitives only; append-only.</summary>
public sealed record SampleRenamedIntegrationEvent(Guid Id, DateTimeOffset OccurredOnUtc, Guid SampleId, string Name)
    : IntegrationEvent(Id, OccurredOnUtc);
