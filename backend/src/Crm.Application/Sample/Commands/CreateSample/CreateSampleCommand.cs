// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Application.Common.Messaging;

namespace Crm.Application.Sample.Commands.CreateSample;

public sealed record CreateSampleCommand(string Name, IReadOnlyList<string> Lines) : ICommand<SampleAggregateDto>;
