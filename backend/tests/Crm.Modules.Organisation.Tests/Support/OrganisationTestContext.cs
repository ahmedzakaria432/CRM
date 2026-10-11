using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;

namespace Crm.Modules.Organisation.Tests.Support;

/// <summary>The module's real <see cref="OrganisationDbContext"/> over the EF Core InMemory provider. Two requests over one
/// store are two contexts with the same database name and different <see cref="ICurrentUserService"/> substitutes.</summary>
public static class OrganisationTestContext
{
    public static string NewDatabaseName() => Guid.NewGuid().ToString("N");

    public static OrganisationDbContext Create(string databaseName, ICurrentUserService user)
    {
        ArgumentNullException.ThrowIfNull(user);

        DbContextOptions<OrganisationDbContext> options = new DbContextOptionsBuilder<OrganisationDbContext>()
            .UseInMemoryDatabase(databaseName)
            // InMemory has no transactions: BeginTransaction throws unless this warning is ignored.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new AuditingSaveChangesInterceptor(user, Clock.Fixed()))
            .Options;

        return new OrganisationDbContext(options, user);
    }

    /// <summary>A context for seeding and for reading back what a test wrote: sees every row, writes anywhere.</summary>
    public static OrganisationDbContext Store(string databaseName) => Create(databaseName, CurrentUsers.Unrestricted());

    public static Repository<TAggregate, TId, OrganisationDbContext> Repo<TAggregate, TId>(OrganisationDbContext context)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull =>
        new(context);
}
