// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

namespace Crm.Modules.Sample.Application.Queries.GetSample;

public sealed record SampleDto(string Name, DateTimeOffset ServerTimeUtc);
