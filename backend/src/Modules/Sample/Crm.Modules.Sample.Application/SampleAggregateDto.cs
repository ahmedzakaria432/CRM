// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.Modules.Sample.Domain;

namespace Crm.Modules.Sample.Application;

/// <summary>Shared across CreateSample, GetSampleById, ListSamples, and RenameSample - all return the same
/// shape, so one DTO here beats near-identical records. Never crosses back into a domain type; FromDomain is
/// the one place that touches SampleAggregate. TenantId/BranchId/DepartmentId are primitives (Story 06) -
/// never a SharedKernel id type across the API boundary.</summary>
public sealed record SampleAggregateDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> Lines,
    Guid TenantId,
    Guid BranchId,
    Guid DepartmentId,
    byte[] RowVersion,
    string? CreatedBy,
    DateTimeOffset? CreatedAtUtc,
    string? ModifiedBy,
    DateTimeOffset? ModifiedAtUtc)
{
    public static SampleAggregateDto FromDomain(SampleAggregate sample) => new(
        sample.Id.Value,
        sample.Name.Value,
        sample.Lines.Select(l => l.Text).ToList(),
        sample.TenantId.Value,
        sample.BranchId.Value,
        sample.DepartmentId.Value,
        sample.RowVersion,
        sample.CreatedBy,
        sample.CreatedAtUtc,
        sample.ModifiedBy,
        sample.ModifiedAtUtc);
}
