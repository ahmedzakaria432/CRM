using Crm.Domain.Sample;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

// CANONICAL SLICE - deleted alongside the rest of Crm.Domain/Sample in Story 11.
public sealed class DatabaseSeeder(CrmDbContext context)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Keyed on a stable well-known id, never on a freshly generated Guid - a fresh Guid can
        // never match on a second run, which would make the seeder insert duplicates every time.
        bool alreadySeeded = await context.Samples
            .IgnoreQueryFilters([CrmDbContext.SoftDeleteFilterName])
            .AnyAsync(s => s.Id == WellKnownIds.SampleSeed, cancellationToken);

        if (alreadySeeded)
        {
            return;
        }

        SampleAggregate seed = SampleAggregate.Create(
            SampleName.Create("Seed sample"),
            ["Seed line one", "Seed line two"],
            WellKnownIds.SampleSeed);

        context.Samples.Add(seed);
        await context.SaveChangesAsync(cancellationToken);
    }
}
