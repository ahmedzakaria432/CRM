// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Application.Common.Messaging;

namespace Crm.Application.Sample.Commands.RenameSample;

/// <summary>RowVersion is the client's last-seen value (base64 byte[] over the wire) - the handler
/// sets it as the expected value before saving, which is what turns a stale write into 409 instead
/// of a silent overwrite.</summary>
public sealed record RenameSampleCommand(Guid Id, string Name, byte[] RowVersion) : ICommand<SampleAggregateDto>;
