using Crm.Modules.Customers.Application.Abstractions;
using Crm.Modules.Customers.Contracts.DevSeed;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Infrastructure.Persistence;
using Crm.Modules.Customers.Tests.Support;
using Crm.Modules.Organisation.Contracts.DevSeed;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Customers.Tests.Infrastructure;

public sealed class CustomersSeederTests
{
    private readonly string _database = CustomersTestContext.NewDatabaseName();
    private readonly ICustomerNumberGenerator _numbers = Stubs.Numbers();

    private async Task Seed()
    {
        await using CustomersDbContext context = CustomersTestContext.Store(_database);
        await new CustomersSeeder(context, _numbers, Clock.Fixed()).SeedAsync(Stubs.Ct);
    }

    private async Task<List<Customer>> Read()
    {
        await using CustomersDbContext context = CustomersTestContext.Store(_database);
        return await context.Customers.AsNoTracking().OrderBy(c => c.Number.Value).ToListAsync(Stubs.Ct);
    }

    private static Customer Of(List<Customer> customers, Guid id) => customers.Single(c => c.Id.Value == id);

    [Fact]
    public async Task ItSeedsSixCustomersWithTheWellKnownIdsAndNumbersInCallOrder()
    {
        await Seed();

        List<Customer> customers = await Read();
        Assert.Equal(6, customers.Count);
        Assert.Equal(
            [
                (CustomersDevSeed.AhmedCustomerId, "C-000001"),
                (CustomersDevSeed.NajdLogisticsCustomerId, "C-000002"),
                (CustomersDevSeed.RedSeaTradingCustomerId, "C-000003"),
                (CustomersDevSeed.AlNoorClinicsCustomerId, "C-000004"),
                (CustomersDevSeed.FaisalCustomerId, "C-000005"),
                (CustomersDevSeed.GulfRetailCustomerId, "C-000006"),
            ],
            customers.Select(c => (c.Id.Value, c.Number.Value)));
    }

    [Fact]
    public async Task TheTypesLanguagesAndDepartmentsMatchTheDemoStory()
    {
        await Seed();

        List<Customer> customers = await Read();
        var tenant = new TenantId(OrganisationDevSeed.TenantId);
        Assert.All(customers, c => Assert.Equal(tenant, c.TenantId));
        (Guid Id, CustomerType Type, string Language, Guid Department)[] expected =
        [
            (CustomersDevSeed.AhmedCustomerId, CustomerType.Individual, "ar", OrganisationDevSeed.RiyadhCareDepartmentId),
            (CustomersDevSeed.NajdLogisticsCustomerId, CustomerType.Organisation, "en", OrganisationDevSeed.RiyadhCareDepartmentId),
            (CustomersDevSeed.RedSeaTradingCustomerId, CustomerType.Organisation, "ar", OrganisationDevSeed.JeddahCareDepartmentId),
            (CustomersDevSeed.AlNoorClinicsCustomerId, CustomerType.Organisation, "ar", OrganisationDevSeed.RiyadhCareDepartmentId),
            (CustomersDevSeed.FaisalCustomerId, CustomerType.Individual, "en", OrganisationDevSeed.RiyadhCareDepartmentId),
            (CustomersDevSeed.GulfRetailCustomerId, CustomerType.Organisation, "en", OrganisationDevSeed.RiyadhBillingDepartmentId),
        ];
        foreach ((Guid id, CustomerType type, string language, Guid department) in expected)
        {
            Customer customer = Of(customers, id);
            Assert.Equal(type, customer.Type);
            Assert.Equal(language, customer.PreferredLanguage.Value);
            Assert.Equal(department, customer.DepartmentId.Value);
        }

        Assert.Equal(OrganisationDevSeed.JeddahBranchId, Of(customers, CustomersDevSeed.RedSeaTradingCustomerId).BranchId.Value);
        Assert.Equal(OrganisationDevSeed.RiyadhBranchId, Of(customers, CustomersDevSeed.GulfRetailCustomerId).BranchId.Value);
        Assert.Equal(4, customers.Count(c => c.DepartmentId.Value == OrganisationDevSeed.RiyadhCareDepartmentId));
    }

    [Fact]
    public async Task TheContactsKeepTheirWellKnownIdsAndPortalFlags()
    {
        await Seed();

        List<Customer> customers = await Read();
        Contact ahmed = Of(customers, CustomersDevSeed.AhmedCustomerId).Contacts.Single();
        Assert.Equal(CustomersDevSeed.AhmedContactId, ahmed.Id);
        Assert.True(ahmed.IsPrimary);
        Assert.True(ahmed.HasPortalAccess);
        Assert.Equal(CustomersDevSeed.AhmedEmail, ahmed.Email?.Value);
        Assert.Equal(CustomersDevSeed.AhmedPhone, ahmed.Phone?.Value);

        Customer alNoor = Of(customers, CustomersDevSeed.AlNoorClinicsCustomerId);
        Assert.Equal(3, alNoor.Contacts.Count);
        Contact mona = alNoor.Contacts.Single(c => c.Id == CustomersDevSeed.AlNoorMonaContactId);
        Assert.True(mona.IsPrimary && mona.HasPortalAccess);
        Contact yousef = alNoor.Contacts.Single(c => c.Id == CustomersDevSeed.AlNoorYousefContactId);
        Assert.True(!yousef.IsPrimary && yousef.HasPortalAccess);
        Contact huda = alNoor.Contacts.Single(c => c.Id == CustomersDevSeed.AlNoorHudaContactId);
        Assert.False(huda.HasPortalAccess);
        Assert.Equal(CustomersDevSeed.AlNoorHudaEmail, huda.Email?.Value);
    }

    [Fact]
    public async Task NajdHasTwoContactsAndAPinnedNoteByTheAdministrator()
    {
        await Seed();

        Customer najd = Of(await Read(), CustomersDevSeed.NajdLogisticsCustomerId);

        Assert.Equal(2, najd.Contacts.Count);
        Assert.Equal(CustomersDevSeed.NajdSaraContactId, najd.Contacts.Single(c => c.IsPrimary).Id);
        Assert.NotNull(najd.Contacts.Single(c => c.Id == CustomersDevSeed.NajdOmarContactId).Phone);
        CustomerNote note = Assert.Single(najd.Notes);
        Assert.Equal(CustomersDevSeed.NajdPinnedNoteId, note.Id);
        Assert.True(note.IsPinned);
        Assert.Equal(Clock.Now, note.PinnedAtUtc);
        Assert.Equal(Clock.Now, note.CreatedAtUtc);
        Assert.Equal(OrganisationDevSeed.AdminUserId, note.AuthorId);
        Assert.Equal("Najd Logistics Company Ltd.", najd.LegalName);
    }

    [Fact]
    public async Task ASecondRunAddsNothingAndNeverDrawsANumber()
    {
        await Seed();
        _numbers.ClearReceivedCalls();

        await Seed();

        Assert.Equal(6, (await Read()).Count);
        await _numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnlyMissingCustomersAreCreatedAndTheGeneratorIsCalledOncePerNewOne()
    {
        await CustomersTestContext.Seed(_database, CustomerFixtures.Individual(99, id: new CustomerId(CustomersDevSeed.AhmedCustomerId)));

        await Seed();

        List<Customer> customers = await Read();
        Assert.Equal(6, customers.Count);
        Assert.Equal("C-000099", Of(customers, CustomersDevSeed.AhmedCustomerId).Number.Value);
        await _numbers.Received(5).NextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ASoftDeletedSeedCustomerIsNotRecreated()
    {
        await Seed();
        await using (CustomersDbContext context = CustomersTestContext.Store(_database))
        {
            context.Customers.Remove(await context.Customers.SingleAsync(c => c.Id == new CustomerId(CustomersDevSeed.FaisalCustomerId), Stubs.Ct));
            await context.SaveChangesAsync(Stubs.Ct);
        }

        await Seed();

        await using CustomersDbContext all = CustomersTestContext.Store(_database);
        Assert.Equal(6, await all.Customers.IgnoreQueryFilters().CountAsync(Stubs.Ct));
        Assert.Equal(5, await all.Customers.CountAsync(Stubs.Ct));
    }

    [Fact]
    public async Task ASeededRowIsStampedByTheAuditingInterceptorAsSystem()
    {
        await Seed();

        Customer ahmed = Of(await Read(), CustomersDevSeed.AhmedCustomerId);

        Assert.Equal(Clock.Now, ahmed.CreatedAtUtc);
    }
}
