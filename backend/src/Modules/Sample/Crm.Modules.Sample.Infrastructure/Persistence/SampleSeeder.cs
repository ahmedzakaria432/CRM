// CANONICAL SLICE - deleted alongside the rest of the Sample module in Story 11.

using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.Modules.Organisation.Contracts.DevSeed;
using Crm.Modules.Sample.Domain;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Sample.Infrastructure.Persistence;

public sealed class SampleSeeder(SampleDbContext context) : IModuleSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Keyed on a stable well-known id, never on a freshly generated Guid - a fresh Guid can
        // never match on a second run, which would make the seeder insert duplicates every time.
        bool alreadySeeded = await context.Samples
            .IgnoreQueryFilters([ModuleDbContext.SoftDeleteFilterName, ModuleDbContext.ScopeFilterName])
            .AnyAsync(s => s.Id == WellKnownIds.SampleSeed, cancellationToken);

        if (alreadySeeded)
        {
            return;
        }

        // Story 06: the seed row belongs to the dev seed's Riyadh Customer Care department (seeders run with
        // no HttpContext, so ICurrentUserService.Scope is Unrestricted and the scope filter is a no-op here).
        SampleAggregate seed = SampleAggregate.Create(
            SampleName.Create("Seed sample"),
            ["Seed line one", "Seed line two"],
            new TenantId(OrganisationDevSeed.TenantId),
            new BranchId(OrganisationDevSeed.RiyadhBranchId),
            new DepartmentId(OrganisationDevSeed.RiyadhCareDepartmentId),
            WellKnownIds.SampleSeed);

        context.Samples.Add(seed);
        await context.SaveChangesAsync(cancellationToken);
    }
}
