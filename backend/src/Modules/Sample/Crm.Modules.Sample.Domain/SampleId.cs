// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.SharedKernel;

namespace Crm.Modules.Sample.Domain;

public sealed record SampleId(Guid Value) : StronglyTypedId(Value)
{
    public static SampleId New() => new(Guid.NewGuid());
}
