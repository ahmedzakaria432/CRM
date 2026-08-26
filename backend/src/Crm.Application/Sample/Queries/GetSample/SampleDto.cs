// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

namespace Crm.Application.Sample.Queries.GetSample;

public sealed record SampleDto(string Name, DateTimeOffset ServerTimeUtc);
