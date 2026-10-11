using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Infrastructure.Persistence;
using Crm.Modules.Customers.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Customers.Tests.Infrastructure;

public sealed class CustomersDbContextTests
{
    private readonly string _database = CustomersTestContext.NewDatabaseName();

    private static Customer Rich(CustomerId? id = null, long sequence = 1)
    {
        Customer customer = CustomerFixtures.Organisation(sequence, "نجد", "Najd", "Najd Co", "ar", id: id ?? Ids.Customer1);
        customer.AddContact("Sara", "Boss", CustomerFixtures.Email("sara@example.com"), CustomerFixtures.Phone("+966500000001"), true, true, Ids.Contact1);
        customer.AddContact("Omar", null, null, CustomerFixtures.Phone("+966500000002"), false, false, Ids.Contact2);
        customer.AddContact("Huda", null, CustomerFixtures.Email("huda@example.com"), null, false, false, Ids.Contact3);
        customer.AddNote("Pinned note", Ids.Author, Clock.Later(1), Ids.Note1);
        customer.AddNote("Other note", Ids.Author, Clock.Later(2), Ids.Note2);
        customer.PinNote(Ids.Note1, Clock.Later(3));
        return customer;
    }

    [Fact]
    public void TheModelKnowsTheCustomersSchemaAndTheNumberSequence()
    {
        using CustomersDbContext context = CustomersTestContext.Store(_database);

        Assert.Equal("customers", CustomersDbContext.SchemaName);
        Assert.Equal("CustomerNumbers", CustomersDbContext.CustomerNumberSequence);
        Assert.Equal("customers", context.Model.GetDefaultSchema());
        Assert.NotNull(context.Model.FindSequence(CustomersDbContext.CustomerNumberSequence, CustomersDbContext.SchemaName));
        Assert.IsAssignableFrom<ICustomersUnitOfWork>(context);
    }

    [Fact]
    public void OnlyTheAggregateRootHasASet()
    {
        using CustomersDbContext context = CustomersTestContext.Store(_database);

        Assert.NotNull(context.Customers);
        Assert.Single(typeof(CustomersDbContext).GetProperties(), p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));
    }

    [Fact]
    public async Task ACustomerWithContactsAndNotesRoundTrips()
    {
        await CustomersTestContext.Seed(_database, Rich());

        await using CustomersDbContext read = CustomersTestContext.Store(_database);
        Customer loaded = await read.Customers.SingleAsync(Stubs.Ct);

        Assert.Equal(Ids.Customer1, loaded.Id);
        Assert.Equal("C-000001", loaded.Number.Value);
        Assert.Equal("نجد", loaded.DisplayName.Arabic);
        Assert.Equal("Najd", loaded.DisplayName.English);
        Assert.Same(PreferredLanguage.Arabic, loaded.PreferredLanguage);
        Assert.Equal(CustomerType.Organisation, loaded.Type);
        Assert.Equal(CustomerStatus.Active, loaded.Status);
        Assert.Equal("Najd Co", loaded.LegalName);
        Assert.Equal(3, loaded.Contacts.Count);
        Assert.Equal(2, loaded.Notes.Count);
        Assert.Equal(3, loaded.Revision);
        Assert.False(loaded.IsDeleted);
    }

    [Fact]
    public async Task TheContactFlagsAndChannelsSurviveIncludingAbsentOnes()
    {
        await CustomersTestContext.Seed(_database, Rich());

        await using CustomersDbContext read = CustomersTestContext.Store(_database);
        Customer loaded = await read.Customers.SingleAsync(Stubs.Ct);

        Contact sara = loaded.Contacts.Single(c => c.Id == Ids.Contact1);
        Assert.True(sara.IsPrimary);
        Assert.True(sara.HasPortalAccess);
        Assert.Equal("sara@example.com", sara.Email?.Value);
        Assert.Equal("+966500000001", sara.Phone?.Value);
        Assert.Equal("Boss", sara.JobTitle);
        Contact omar = loaded.Contacts.Single(c => c.Id == Ids.Contact2);
        Assert.Null(omar.Email);
        Assert.Equal("+966500000002", omar.Phone?.Value);
        Contact huda = loaded.Contacts.Single(c => c.Id == Ids.Contact3);
        Assert.Equal("huda@example.com", huda.Email?.Value);
        Assert.Null(huda.Phone);
    }

    [Fact]
    public async Task TheNotesKeepTheirPinState()
    {
        await CustomersTestContext.Seed(_database, Rich());

        await using CustomersDbContext read = CustomersTestContext.Store(_database);
        Customer loaded = await read.Customers.SingleAsync(Stubs.Ct);

        CustomerNote pinned = loaded.Notes.Single(n => n.Id == Ids.Note1);
        Assert.True(pinned.IsPinned);
        Assert.Equal(Clock.Later(3), pinned.PinnedAtUtc);
        Assert.Equal(Clock.Later(1), pinned.CreatedAtUtc);
        Assert.Equal(Ids.Author, pinned.AuthorId);
        Assert.False(loaded.Notes.Single(n => n.Id == Ids.Note2).IsPinned);
    }

    [Fact]
    public async Task AContactAddedToATrackedCustomerIsPersisted()
    {
        // InMemory cannot reproduce the relational "UPDATE affected 0 rows" that ValueGeneratedNever guards against;
        // only the outcome is asserted.
        await CustomersTestContext.Seed(_database, Rich());
        await using (CustomersDbContext write = CustomersTestContext.Store(_database))
        {
            Customer tracked = await write.Customers.SingleAsync(Stubs.Ct);
            tracked.AddSimpleContact("New", "new@example.com", id: Ids.Contact4);
            await write.SaveChangesAsync(Stubs.Ct);
        }

        await using CustomersDbContext read = CustomersTestContext.Store(_database);
        Customer loaded = await read.Customers.SingleAsync(Stubs.Ct);
        Assert.Equal(4, loaded.Contacts.Count);
        Assert.Equal(4, loaded.Revision);
    }

    [Fact]
    public async Task TheSoftDeleteFilterHidesADeletedCustomer()
    {
        await CustomersTestContext.Seed(_database, Rich(), CustomerFixtures.Individual(2, id: Ids.Customer2));
        await using (CustomersDbContext write = CustomersTestContext.Store(_database))
        {
            Customer tracked = await write.Customers.SingleAsync(c => c.Id == Ids.Customer2, Stubs.Ct);
            write.Customers.Remove(tracked);
            await write.SaveChangesAsync(Stubs.Ct);
        }

        await using CustomersDbContext read = CustomersTestContext.Store(_database);
        Assert.Equal(Ids.Customer1, Assert.Single(await read.Customers.ToListAsync(Stubs.Ct)).Id);
        Assert.Equal(2, await read.Customers.IgnoreQueryFilters().CountAsync(Stubs.Ct));
    }

    [Fact]
    public async Task TheScopeFilterShowsACallerOnlyTheirDepartmentsCustomers()
    {
        await CustomersTestContext.Seed(
            _database,
            CustomerFixtures.Individual(1, id: Ids.Customer1),
            CustomerFixtures.Individual(2, department: Ids.DepartmentA2, branch: Ids.BranchA2, id: Ids.Customer2),
            CustomerFixtures.Individual(3, tenant: Ids.TenantB, department: Ids.DepartmentB1, branch: Ids.BranchB1, id: Ids.Customer3));

        await using CustomersDbContext a1 = CustomersTestContext.Create(_database, CurrentUsers.Agent(Scopes.AgentA1()));
        await using CustomersDbContext a1AndA2 = CustomersTestContext.Create(_database, CurrentUsers.Agent(Scopes.AgentA1AndA2()));
        await using CustomersDbContext b1 = CustomersTestContext.Create(_database, CurrentUsers.Agent(Scopes.AgentB1()));
        await using CustomersDbContext all = CustomersTestContext.Store(_database);
        await using CustomersDbContext nobody = CustomersTestContext.Create(_database, CurrentUsers.Anonymous());

        Assert.Equal([Ids.Customer1], (await a1.Customers.ToListAsync(Stubs.Ct)).Select(c => c.Id));
        Assert.Equal(2, await a1AndA2.Customers.CountAsync(Stubs.Ct));
        Assert.Equal([Ids.Customer3], (await b1.Customers.ToListAsync(Stubs.Ct)).Select(c => c.Id));
        Assert.Equal(3, await all.Customers.CountAsync(Stubs.Ct));
        Assert.Equal(0, await nobody.Customers.CountAsync(Stubs.Ct));
    }

    [Fact]
    public async Task TheWriteGuardRefusesToSaveACustomerOutsideTheCallersScope()
    {
        await using CustomersDbContext write = CustomersTestContext.Create(_database, CurrentUsers.Agent(Scopes.AgentA1()));
        write.Customers.Add(CustomerFixtures.Individual(1, department: Ids.DepartmentA2, branch: Ids.BranchA2));

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(() => write.SaveChangesAsync(Stubs.Ct));
    }

    [Fact]
    public async Task TheWriteGuardAllowsACustomerInsideTheCallersScope()
    {
        await using CustomersDbContext write = CustomersTestContext.Create(_database, CurrentUsers.Agent(Scopes.AgentA1()));
        write.Customers.Add(CustomerFixtures.Individual(1));

        Assert.True(await write.SaveChangesAsync(Stubs.Ct) > 0);
    }

    [Fact]
    public async Task TheAuditingInterceptorStampsTheCreatorAndTheClock()
    {
        await using (CustomersDbContext write = CustomersTestContext.Create(_database, CurrentUsers.Agent(Scopes.AgentA1()), Clock.Later(15)))
        {
            write.Customers.Add(CustomerFixtures.Individual(1, id: Ids.Customer1));
            await write.SaveChangesAsync(Stubs.Ct);
        }

        await using CustomersDbContext read = CustomersTestContext.Store(_database);
        Customer loaded = await read.Customers.SingleAsync(Stubs.Ct);
        Assert.Equal(CurrentUsers.DefaultUserId, loaded.CreatedBy);
        Assert.Equal(Clock.Later(15), loaded.CreatedAtUtc);
    }
}
