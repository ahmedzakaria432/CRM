using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.AddContact;
using Crm.Modules.Customers.Application.Commands.AddNote;
using Crm.Modules.Customers.Application.Commands.DeactivateCustomer;
using Crm.Modules.Customers.Application.Commands.PinNote;
using Crm.Modules.Customers.Application.Commands.ReactivateCustomer;
using Crm.Modules.Customers.Application.Commands.RemoveContact;
using Crm.Modules.Customers.Application.Commands.RemoveNote;
using Crm.Modules.Customers.Application.Commands.SetPrimaryContact;
using Crm.Modules.Customers.Application.Commands.UpdateContact;
using Crm.Modules.Customers.Application.Commands.UpdateCustomer;
using Crm.Modules.Customers.Application.Queries.GetCustomerById;
using Crm.Modules.Customers.Application.Queries.GetCustomerNames;
using Crm.Modules.Customers.Application.Queries.LookupCustomers;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Infrastructure.Persistence;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;
using FluentValidation.Results;

namespace Crm.Modules.Customers.Tests.Application.Queries;

public sealed class GetCustomerByIdQueryHandlerTests
{
    private static Customer Full()
    {
        Customer customer = CustomerFixtures.Organisation(7, "نجد", "Najd", "Najd Co", "ar", id: Ids.Customer1);
        customer.AddContact("Zed", "Boss", CustomerFixtures.Email("zed@example.com"), null, false, false, Ids.Contact2);
        customer.AddContact("Alice", null, null, CustomerFixtures.Phone("+966500000001"), true, false, Ids.Contact1);
        customer.AddNote("older", Ids.Author, Clock.Later(1), Ids.Note1);
        customer.AddNote("newer", Ids.Author, Clock.Later(2), Ids.Note2);
        customer.AddNote("pinned", Ids.Author, Clock.Later(0), Ids.Note3);
        customer.PinNote(Ids.Note3, Clock.Later(5));
        return customer;
    }

    [Fact]
    public async Task AFoundCustomerIsMappedFieldForField()
    {
        Customer customer = Full();
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        repository.FindsFirstOf(customer);

        Result<CustomerDto> result = await new GetCustomerByIdQueryHandler(repository).Handle(new GetCustomerByIdQuery(Ids.Customer1.Value), Stubs.Ct);

        CustomerDto dto = result.Value;
        Assert.Equal(Ids.Customer1.Value, dto.Id);
        Assert.Equal("C-000007", dto.Number);
        Assert.Equal("Najd", dto.DisplayName.En);
        Assert.Equal("نجد", dto.DisplayName.Ar);
        Assert.Equal("Organisation", dto.Type);
        Assert.Equal("Active", dto.Status);
        Assert.Equal("Najd Co", dto.LegalName);
        Assert.Equal("ar", dto.PreferredLanguage);
        Assert.Equal(Ids.TenantA.Value, dto.TenantId);
        Assert.Equal(Ids.BranchA1.Value, dto.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, dto.DepartmentId);
        Assert.Equal(customer.RowVersion, dto.RowVersion);
        Assert.Equal(customer.CreatedAtUtc, dto.CreatedAtUtc);
    }

    [Fact]
    public async Task ContactsListThePrimaryFirstThenByNameAndNotesThePinnedFirstThenNewestFirst()
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        repository.FindsFirstOf(Full());

        Result<CustomerDto> result = await new GetCustomerByIdQueryHandler(repository).Handle(new GetCustomerByIdQuery(Ids.Customer1.Value), Stubs.Ct);

        Assert.Equal(["Alice", "Zed"], result.Value.Contacts.Select(c => c.Name));
        Assert.Equal(["pinned", "newer", "older"], result.Value.Notes.Select(n => n.Body));
    }

    [Fact]
    public async Task AnUnknownCustomerIsNotFoundAndTheDescriptionNamesTheId()
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        repository.FindsFirstOf();

        Result<CustomerDto> result = await new GetCustomerByIdQueryHandler(repository).Handle(new GetCustomerByIdQuery(Ids.Customer1.Value), Stubs.Ct);

        Expect.Failure(result, "Customers.CustomerNotFound", ErrorType.NotFound);
        Assert.Contains(Ids.Customer1.Value.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSpecificationIsTheByIdSpecificationAndTheCallIsFirstOrDefault()
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        var handler = new GetCustomerByIdQueryHandler(repository);

        await handler.Handle(new GetCustomerByIdQuery(Ids.Customer1.Value), Stubs.Ct);

        await repository.Received(1).FirstOrDefaultAsync(Arg.Is<Specification<Customer>>(s => s is CustomerByIdSpecification), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().GetByIdAsync(Arg.Any<CustomerId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheValidatorRequiresAnId()
    {
        var validator = new GetCustomerByIdQueryValidator();

        Expect.Valid(validator.Validate(new GetCustomerByIdQuery(Ids.Customer1.Value)));
        Expect.Invalid(validator.Validate(new GetCustomerByIdQuery(Guid.Empty)), "Id");
    }
}

public sealed class GetCustomerByIdQueryTests
{
    [Fact]
    public void TheCacheKeyAndTagAreTheCustomerTag()
    {
        var query = new GetCustomerByIdQuery(Ids.Customer1.Value);

        Assert.Equal($"customers:customers:{Ids.Customer1.Value}", query.CacheKey);
        Assert.Equal([query.CacheKey], query.CacheTags);
        Assert.False(((ICacheableQuery)query).VaryByUser);
    }

    [Fact]
    public void TheQueryIsCacheableAndNeedsTheCustomersReadPermission()
    {
        Assert.True(typeof(ICacheableQuery).IsAssignableFrom(typeof(GetCustomerByIdQuery)));
        Assert.Equal("customers.read", Stubs.PermissionOf(typeof(GetCustomerByIdQuery)));
    }

    [Fact]
    public void EveryCommandOnAnExistingCustomerInvalidatesTheTagTheQueryCaches()
    {
        Guid id = Ids.Customer1.Value;
        byte[] version = Ids.RowVersion();
        ICacheInvalidatingCommand[] commands =
        [
            new UpdateCustomerCommand(id, "a", "a", "Individual", null, "en", version),
            new DeactivateCustomerCommand(id, version),
            new ReactivateCustomerCommand(id, version),
            new AddContactCommand(id, "a", null, "a@b.co", null, false, false, version),
            new UpdateContactCommand(id, Ids.Contact1, "a", null, "a@b.co", null, false, version),
            new SetPrimaryContactCommand(id, Ids.Contact1, version),
            new RemoveContactCommand(id, Ids.Contact1, version),
            new AddNoteCommand(id, "n"),
            new PinNoteCommand(id, Ids.Note1, true),
            new RemoveNoteCommand(id, Ids.Note1),
        ];
        string tag = Assert.Single(new GetCustomerByIdQuery(id).CacheTags);

        Assert.All(commands, command => Assert.Contains(tag, command.CacheTagsToInvalidate));
    }
}

public sealed class LookupCustomersQueryHandlerTests
{
    private static async Task<IReadOnlyList<CustomerLookupDto>> Lookup(SeedGraph graph, LookupCustomersQuery query, OrganisationScope scope)
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        repository.Lists(await graph.Visible(scope));

        Result<IReadOnlyList<CustomerLookupDto>> result = await new LookupCustomersQueryHandler(repository).Handle(query, Stubs.Ct);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public async Task OnlyActiveCustomersAreListedOrderedByEnglishNameThenNumber()
    {
        SeedGraph graph = await SeedGraph.Create();

        IReadOnlyList<CustomerLookupDto> found = await Lookup(graph, new LookupCustomersQuery(null), Scopes.AgentA1AndA2());

        Assert.Equal(["C-000001", "C-000004", "C-000002"], found.Select(c => c.Number));
    }

    [Theory]
    [InlineData("Najd", "C-000002")]
    [InlineData("C-000004", "C-000004")]
    [InlineData("sara@najd", "C-000002")]
    [InlineData("966 (55) 1112", "C-000002")]
    [InlineData("أحمد", "C-000001")]
    public async Task TheSearchMatchesNamesNumberEmailAndPhoneWithSeparatorsStripped(string term, string expected)
    {
        SeedGraph graph = await SeedGraph.Create();

        IReadOnlyList<CustomerLookupDto> found = await Lookup(graph, new LookupCustomersQuery(term), Scopes.AgentA1AndA2());

        Assert.Equal([expected], found.Select(c => c.Number));
    }

    [Fact]
    public async Task AnInactiveCustomerNeverMatchesEvenByNumber()
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Empty(await Lookup(graph, new LookupCustomersQuery("C-000003"), Scopes.AgentA1AndA2()));
    }

    [Fact]
    public async Task TakeLimitsTheCount()
    {
        SeedGraph graph = await SeedGraph.Create();

        IReadOnlyList<CustomerLookupDto> found = await Lookup(graph, new LookupCustomersQuery(null, 2), Scopes.AgentA1AndA2());

        Assert.Equal(["C-000001", "C-000004"], found.Select(c => c.Number));
        Assert.Equal(10, new LookupCustomersQuery(null).Take);
    }

    [Fact]
    public async Task TheScopeIsolatesTheCaller()
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Equal(["C-000005"], (await Lookup(graph, new LookupCustomersQuery(null), Scopes.AgentB1())).Select(c => c.Number));
    }

    [Fact]
    public async Task TheDtoCarriesThePrimaryEmailAndPhoneOrNulls()
    {
        SeedGraph graph = await SeedGraph.Create();

        IReadOnlyList<CustomerLookupDto> found = await Lookup(graph, new LookupCustomersQuery(null), Scopes.AgentA1());

        CustomerLookupDto ahmed = found[0];
        Assert.Equal("ahmed@example.com", ahmed.PrimaryEmail);
        Assert.Equal("+966501234567", ahmed.PrimaryPhone);
        Assert.Equal("Ahmed Al-Qahtani", ahmed.DisplayName.En);
        CustomerLookupDto faisal = found[1];
        Assert.Null(faisal.PrimaryEmail);
        Assert.Null(faisal.PrimaryPhone);
    }

    [Fact]
    public void TheQueryNeedsTheCustomersReadPermission() =>
        Assert.Equal("customers.read", Stubs.PermissionOf(typeof(LookupCustomersQuery)));
}

public sealed class LookupCustomersQueryValidatorTests
{
    private readonly LookupCustomersQueryValidator _validator = new();

    private ValidationResult Validate(string? search = null, int take = 10) => _validator.Validate(new LookupCustomersQuery(search, take));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(25, true)]
    [InlineData(26, false)]
    public void TakeIsOneToTwentyFive(int take, bool valid) => Assert.Equal(valid, Validate(take: take).IsValid);

    [Theory]
    [InlineData(null, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void TheSearchIsLimitedToTwoHundredCharacters(object? input, bool valid) =>
        Assert.Equal(valid, Validate(search: input is int length ? new string('s', length) : null).IsValid);
}

public sealed class GetCustomerNamesQueryHandlerTests
{
    [Fact]
    public async Task NoIdsIsAnEmptySuccessWithoutTouchingTheRepository()
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();

        Result<IReadOnlyList<CustomerNameSummary>> result = await new GetCustomerNamesQueryHandler(repository).Handle(new GetCustomerNamesQuery([]), Stubs.Ct);

        Assert.Empty(result.Value);
        await repository.DidNotReceive().ListAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheIdsAreDistinctedBeforeTheSpecification()
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        Specification<Customer>? seen = null;
        repository.ListAsync(Arg.Do<Specification<Customer>>(s => seen = s), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Customer>>([]));
        Customer one = CustomerFixtures.Individual(1, id: Ids.Customer1);
        Customer two = CustomerFixtures.Individual(2, id: Ids.Customer2);

        await new GetCustomerNamesQueryHandler(repository).Handle(
            new GetCustomerNamesQuery([Ids.Customer1.Value, Ids.Customer1.Value, Ids.Customer1.Value]), Stubs.Ct);

        CustomersByIdsSpecification spec = Assert.IsType<CustomersByIdsSpecification>(seen);
        Assert.Equal([one], Stubs.Matching(spec, one, two));
    }

    [Fact]
    public async Task TheNumberAndBothNamesAreReturned()
    {
        SeedGraph graph = await SeedGraph.Create();
        await using CustomersDbContext context = graph.Context(Scopes.AgentA1());
        var handler = new GetCustomerNamesQueryHandler(CustomersTestContext.Repo(context));

        Result<IReadOnlyList<CustomerNameSummary>> result = await handler.Handle(new GetCustomerNamesQuery([graph.Najd.Id.Value, graph.Najd.Id.Value]), Stubs.Ct);

        CustomerNameSummary name = Assert.Single(result.Value);
        Assert.Equal(graph.Najd.Id.Value, name.Id);
        Assert.Equal("C-000002", name.Number);
        Assert.Equal("Najd Logistics", name.DisplayNameEn);
        Assert.Equal("نجد", name.DisplayNameAr);
    }

    [Fact]
    public async Task UnknownOutOfScopeAndDeletedIdsAreAbsentNeverAnError()
    {
        SeedGraph graph = await SeedGraph.Create();
        await using CustomersDbContext context = graph.Context(Scopes.AgentA1());
        var handler = new GetCustomerNamesQueryHandler(CustomersTestContext.Repo(context));

        Result<IReadOnlyList<CustomerNameSummary>> result = await handler.Handle(
            new GetCustomerNamesQuery([graph.Ahmed.Id.Value, graph.RedSea.Id.Value, graph.Foreign.Id.Value, graph.Deleted.Id.Value, Ids.Missing]), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(graph.Ahmed.Id.Value, Assert.Single(result.Value).Id);
    }

    [Fact]
    public async Task AHundredIdsAreAcceptedAndListedOnce()
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        repository.Lists();
        Guid[] hundred = [.. Enumerable.Range(1, 100).Select(_ => Guid.NewGuid())];

        Result<IReadOnlyList<CustomerNameSummary>> result = await new GetCustomerNamesQueryHandler(repository).Handle(new GetCustomerNamesQuery(hundred), Stubs.Ct);

        Assert.True(result.IsSuccess);
        await repository.Received(1).ListAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheQueryNeedsTheCustomersReadPermission() =>
        Assert.Equal("customers.read", Stubs.PermissionOf(typeof(GetCustomerNamesQuery)));
}

public sealed class GetCustomerNamesQueryValidatorTests
{
    private readonly GetCustomerNamesQueryValidator _validator = new();

    [Theory]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void AtMostAHundredIdsAreAccepted(int count, bool valid)
    {
        ValidationResult result = _validator.Validate(new GetCustomerNamesQuery([.. Enumerable.Range(1, count).Select(_ => Guid.NewGuid())]));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void ANullListMakesTheValidatorThrowRatherThanFail()
    {
        // Pinned (Follow-up): NotNull does not stop the chain, so the Must predicate dereferences null.
        var query = (GetCustomerNamesQuery)typeof(GetCustomerNamesQuery).GetConstructors()[0].Invoke([null]);

        Assert.Throws<NullReferenceException>(() => _validator.Validate(query));
    }
}
