using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Customers.Infrastructure.Persistence;

public sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options, ICurrentUserService currentUser)
    : ModuleDbContext(options, currentUser), ICustomersUnitOfWork
{
    public const string SchemaName = "customers";

    /// <summary>customers.CustomerNumbers - SqlCustomerNumberGenerator's SQL names it literally.</summary>
    public const string CustomerNumberSequence = "CustomerNumbers";

    protected override string Schema => SchemaName;

    // Aggregate roots only. Contact and CustomerNote have no DbSet - reachable only through Customer.
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The one model element that is not an entity type, so no IEntityTypeConfiguration can declare it.
        modelBuilder.HasSequence<long>(CustomerNumberSequence, SchemaName).StartsAt(1).IncrementsBy(1);
    }
}
