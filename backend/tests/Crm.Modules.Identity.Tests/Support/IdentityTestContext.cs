using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.Modules.Identity.Infrastructure.Persistence;
using Crm.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;

namespace Crm.Modules.Identity.Tests.Support;

/// <summary>The module's real <see cref="IdentityDbContext"/> over the EF Core InMemory provider.</summary>
public static class IdentityTestContext
{
    public static string NewDatabaseName() => Guid.NewGuid().ToString("N");

    public static IdentityDbContext Create(string databaseName, ICurrentUserService user)
    {
        ArgumentNullException.ThrowIfNull(user);

        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new AuditingSaveChangesInterceptor(user, Clock.Fixed()))
            .Options;

        return new IdentityDbContext(options, user);
    }

    public static IdentityDbContext Store(string databaseName) => Create(databaseName, CurrentUsers.Unrestricted());

    public static Repository<TAggregate, TId, IdentityDbContext> Repo<TAggregate, TId>(IdentityDbContext context)
        where TAggregate : AggregateRoot<TId>, IHasRowVersion
        where TId : notnull =>
        new(context);
}
