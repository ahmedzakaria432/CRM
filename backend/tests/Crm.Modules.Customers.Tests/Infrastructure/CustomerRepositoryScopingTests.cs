using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Infrastructure.Persistence;
using Crm.Modules.Customers.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Customers.Tests.Infrastructure;

/// <summary>The generic repository behaviour is the building blocks'; only the Customers graph and scope marker are asserted.</summary>
public sealed class CustomerRepositoryScopingTests
{
    private readonly string _database = CustomersTestContext.NewDatabaseName();

    private static Customer InDepartmentA1()
    {
        Customer customer = CustomerFixtures.Individual(1, id: Ids.Customer1);
        customer.AddSimpleContact("Sara", "sara@example.com", "+966500000001");
        customer.AddNote("a note", Ids.Author, Clock.Now, Ids.Note1);
        return customer;
    }

    private async Task<Repository<Customer, CustomerId, CustomersDbContext>> RepositoryFor(Crm.BuildingBlocks.Application.Abstractions.OrganisationScope scope)
    {
        await CustomersTestContext.Seed(_database, InDepartmentA1());
        return CustomersTestContext.Repo(CustomersTestContext.Create(_database, CurrentUsers.Agent(scope)));
    }

    [Fact]
    public async Task AnInScopeCustomerIsReturnedWithItsContactsAndNotes()
    {
        Repository<Customer, CustomerId, CustomersDbContext> repository = await RepositoryFor(Scopes.AgentA1());

        Customer? loaded = await repository.GetByIdAsync(Ids.Customer1, Stubs.Ct);

        Assert.NotNull(loaded);
        Assert.Single(loaded.Contacts);
        Assert.Single(loaded.Notes);
    }

    [Fact]
    public async Task AnUnknownIdIsNull()
    {
        Repository<Customer, CustomerId, CustomersDbContext> repository = await RepositoryFor(Scopes.AgentA1());

        Assert.Null(await repository.GetByIdAsync(new CustomerId(Ids.Missing), Stubs.Ct));
    }

    [Fact]
    public async Task AnExistingCustomerOutsideTheCallersScopeThrowsTheScopeViolationNotNull()
    {
        Repository<Customer, CustomerId, CustomersDbContext> otherDepartment = await RepositoryFor(Scopes.AgentA2Only());

        OrganisationScopeViolationException exception = await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => otherDepartment.GetByIdAsync(Ids.Customer1, Stubs.Ct));

        Assert.Contains("Customer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnotherTenantsCustomerThrowsTheScopeViolationToo()
    {
        Repository<Customer, CustomerId, CustomersDbContext> otherTenant = await RepositoryFor(Scopes.AgentB1());

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(() => otherTenant.GetByIdAsync(Ids.Customer1, Stubs.Ct));
    }

    [Fact]
    public async Task ASoftDeletedCustomerIsNull()
    {
        await CustomersTestContext.Seed(_database, InDepartmentA1());
        await using (CustomersDbContext write = CustomersTestContext.Store(_database))
        {
            write.Customers.Remove(await write.Customers.SingleAsync(Stubs.Ct));
            await write.SaveChangesAsync(Stubs.Ct);
        }

        await using CustomersDbContext read = CustomersTestContext.Create(_database, CurrentUsers.Agent(Scopes.AgentA1()));

        Assert.Null(await CustomersTestContext.Repo(read).GetByIdAsync(Ids.Customer1, Stubs.Ct));
    }

    [Fact]
    public async Task TheOriginalRowVersionIsSetOnTheTrackedEntry()
    {
        await CustomersTestContext.Seed(_database, InDepartmentA1());
        await using CustomersDbContext context = CustomersTestContext.Create(_database, CurrentUsers.Agent(Scopes.AgentA1()));
        Repository<Customer, CustomerId, CustomersDbContext> repository = CustomersTestContext.Repo(context);
        Customer customer = await repository.GetByIdAsync(Ids.Customer1, Stubs.Ct) ?? throw new InvalidOperationException("seeded customer missing");

        repository.SetOriginalRowVersion(customer, Ids.RowVersion());

        Assert.Equal(Ids.RowVersion(), context.Entry(customer).Property(nameof(Customer.RowVersion)).OriginalValue);
    }

    [Fact]
    public async Task AddAndSaveChangesPersistsTheCustomer()
    {
        await using (CustomersDbContext write = CustomersTestContext.Create(_database, CurrentUsers.Agent(Scopes.AgentA1())))
        {
            CustomersTestContext.Repo(write).Add(InDepartmentA1());
            await write.SaveChangesAsync(Stubs.Ct);
        }

        await using CustomersDbContext read = CustomersTestContext.Store(_database);
        Assert.Equal(1, await read.Customers.CountAsync(Stubs.Ct));
    }
}
