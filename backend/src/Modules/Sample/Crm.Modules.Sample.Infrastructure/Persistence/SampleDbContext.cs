// CANONICAL SLICE - every module has exactly one DbContext shaped like this one.
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.Modules.Sample.Application;
using Crm.Modules.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Sample.Infrastructure.Persistence;

public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options)
    : ModuleDbContext(options), ISampleUnitOfWork
{
    public const string SchemaName = "sample";

    protected override string Schema => SchemaName;

    // Aggregate roots only. A child entity never gets a DbSet - CLAUDE.md, "Backend", DDD rules.
    public DbSet<SampleAggregate> Samples => Set<SampleAggregate>();
}
