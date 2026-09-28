// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Sample.Application.Commands.CreateSample;

public sealed record CreateSampleCommand(string Name, IReadOnlyList<string> Lines) : ICommand<SampleAggregateDto>;
