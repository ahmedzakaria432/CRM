// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Application.Common.Specifications;
using Crm.Domain.Sample;

namespace Crm.Application.Sample.Specifications;

public sealed class SampleByIdSpecification : Specification<SampleAggregate>
{
    public SampleByIdSpecification(SampleId id)
    {
        Where(s => s.Id == id);
        AsReadOnly();
    }
}
