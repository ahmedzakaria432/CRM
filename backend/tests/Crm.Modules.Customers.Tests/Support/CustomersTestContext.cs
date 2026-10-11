using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;

namespace Crm.Modules.Customers.Tests.Support;

/// <summary>The module's real <see cref="CustomersDbContext"/> over the EF Core InMemory provider.</summary>
public static class CustomersTestContext
{
    public static string NewDatabaseName() => Guid.NewGuid().ToString("N");

    public static CustomersDbContext Create(string databaseName, ICurrentUserService user, DateTimeOffset? at = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        DbContextOptions<CustomersDbContext> options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new AuditingSaveChangesInterceptor(user, Clock.Fixed(at)))
            .Options;

        return new CustomersDbContext(options, user);
    }

    public static CustomersDbContext Store(string databaseName) => Create(databaseName, CurrentUsers.Unrestricted());

    public static Repository<Customer, CustomerId, CustomersDbContext> Repo(CustomersDbContext context) => new(context);

    /// <summary>Writes the customers into the named database through an unrestricted context.</summary>
    public static Task Seed(string databaseName, params Customer[] customers) => SeedAt(databaseName, Clock.Now, customers);

    /// <summary>Like <see cref="Seed"/>, with the auditing interceptor stamping <paramref name="at"/> as the creation time.</summary>
    public static async Task SeedAt(string databaseName, DateTimeOffset at, params Customer[] customers)
    {
        await using CustomersDbContext store = Create(databaseName, CurrentUsers.Unrestricted(), at);
        store.Customers.AddRange(customers);
        await store.SaveChangesAsync(Stubs.Ct);
    }
}
