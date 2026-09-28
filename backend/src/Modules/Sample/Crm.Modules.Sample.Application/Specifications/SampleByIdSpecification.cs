// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Sample.Domain;

namespace Crm.Modules.Sample.Application.Specifications;

public sealed class SampleByIdSpecification : Specification<SampleAggregate>
{
    public SampleByIdSpecification(SampleId id)
    {
        Where(s => s.Id == id);
        AsReadOnly();
    }
}
