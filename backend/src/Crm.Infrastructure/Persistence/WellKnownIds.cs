using Crm.Domain.Sample;

namespace Crm.Infrastructure.Persistence;

// CANONICAL SLICE - deleted alongside the rest of Crm.Domain/Sample in Story 11.
public static class WellKnownIds
{
    public static readonly SampleId SampleSeed = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
}
