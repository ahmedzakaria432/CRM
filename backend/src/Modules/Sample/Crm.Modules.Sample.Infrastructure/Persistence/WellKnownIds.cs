// CANONICAL SLICE - deleted alongside the rest of the Sample module in Story 11.

using Crm.Modules.Sample.Domain;

namespace Crm.Modules.Sample.Infrastructure.Persistence;

public static class WellKnownIds
{
    public static readonly SampleId SampleSeed = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
}
