// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Domain.Sample;

namespace Crm.Application.Sample;

/// <summary>Shared across CreateSample, GetSampleById, and RenameSample - all three return the same
/// shape, so one DTO here beats three near-identical records. Never crosses back into a domain type;
/// FromDomain is the one place that touches SampleAggregate.</summary>
public sealed record SampleAggregateDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> Lines,
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
        sample.RowVersion,
        sample.CreatedBy,
        sample.CreatedAtUtc,
        sample.ModifiedBy,
        sample.ModifiedAtUtc);
}
