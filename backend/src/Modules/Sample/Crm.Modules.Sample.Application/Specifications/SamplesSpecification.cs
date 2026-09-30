// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Sample.Domain;

namespace Crm.Modules.Sample.Application.Specifications;

/// <summary>THE NAIVE NEW QUERY of Story 06's acceptance criterion: no scope criteria at all. Automatic
/// scoping means this still returns only the caller's tenant/branch/department rows - proven by reading the
/// EF SQL (organisation.http request 20).</summary>
public sealed class SamplesSpecification : Specification<SampleAggregate>
{
    public SamplesSpecification()
    {
        ApplyOrderByDescending(s => s.CreatedAtUtc ?? DateTimeOffset.MinValue);
        ApplyPaging(0, 100);
        AsReadOnly();
    }
}
