// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Application.Common.Messaging;

namespace Crm.Application.Sample.Commands.DeleteSample;

public sealed record DeleteSampleCommand(Guid Id) : ICommand;
