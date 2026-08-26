// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Domain.Common;

namespace Crm.Domain.Sample;

public sealed record SampleRenamed(SampleId SampleId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
