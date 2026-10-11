using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Application.Queries.FindContactsByEmail;
using Crm.Modules.Customers.Application.Queries.FindPortalContactsByEmail;
using Crm.Modules.Customers.Application.Queries.GetPortalContact;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Infrastructure.Persistence;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;
using FluentValidation.Results;

namespace Crm.Modules.Customers.Tests.Application.Queries;

public sealed class FindContactsByEmailQueryHandlerTests
{
    private static async Task<IReadOnlyList<CustomerContactMatch>> Find(SeedGraph graph, string email, OrganisationScope? scope = null)
    {
        await using CustomersDbContext context = graph.Context(scope ?? Scopes.AgentA1AndA2());
        var handler = new FindContactsByEmailQueryHandler(CustomersTestContext.Repo(context));

        Result<IReadOnlyList<CustomerContactMatch>> result = await handler.Handle(new FindContactsByEmailQuery(email), Stubs.Ct);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("")]
    [InlineData("a@b")]
    public async Task AMalformedEmailIsAnEmptySuccessAndTheRepositoryIsNotQueried(string email)
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        var handler = new FindContactsByEmailQueryHandler(repository);

        Result<IReadOnlyList<CustomerContactMatch>> result = await handler.Handle(new FindContactsByEmailQuery(email), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        await repository.DidNotReceive().ListAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheEmailIsNormalisedBeforeTheMatch()
    {
        SeedGraph graph = await SeedGraph.Create();

        IReadOnlyList<CustomerContactMatch> matches = await Find(graph, " AHMED@Example.COM ");

        CustomerContactMatch match = Assert.Single(matches);
        Assert.Equal("ahmed@example.com", match.Email);
    }

    [Fact]
    public async Task AMatchCarriesTheCustomerAndContactDetails()
    {
        SeedGraph graph = await SeedGraph.Create();

        CustomerContactMatch match = Assert.Single(await Find(graph, "ahmed@example.com"));

        Assert.Equal(graph.Ahmed.Id.Value, match.CustomerId);
        Assert.Equal("C-000001", match.CustomerNumber);
        Assert.True(match.IsCustomerActive);
        Assert.Equal(Guid.Parse("c2000000-0000-0000-0000-000000000001"), match.ContactId);
        Assert.Equal("Ahmed", match.ContactName);
        Assert.True(match.HasPortalAccess);
        Assert.Equal(Ids.TenantA.Value, match.TenantId);
        Assert.Equal(Ids.BranchA1.Value, match.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, match.DepartmentId);
    }

    [Fact]
    public async Task AnInactiveCustomerIsIncludedUnlikeThePortalLookup()
    {
        SeedGraph graph = await SeedGraph.Create();

        CustomerContactMatch match = Assert.Single(await Find(graph, "fahad@redsea.example.com"));

        Assert.False(match.IsCustomerActive);
        Assert.Equal("C-000003", match.CustomerNumber);
    }

    [Fact]
    public async Task AContactWithoutPortalAccessIsStillFound()
    {
        SeedGraph graph = await SeedGraph.Create();

        CustomerContactMatch match = Assert.Single(await Find(graph, "sara@najd.example.com"));

        Assert.False(match.HasPortalAccess);
    }

    [Fact]
    public async Task ACustomerOfAnotherDepartmentOrTenantIsInvisible()
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Empty(await Find(graph, "fahad@redsea.example.com", Scopes.AgentA1()));
        Assert.Equal("C-000005", Assert.Single(await Find(graph, "ahmed@example.com", Scopes.AgentB1())).CustomerNumber);
    }

    [Fact]
    public async Task AContactWithoutAnEmailNeverMatchesAndASoftDeletedCustomerIsHidden()
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Empty(await Find(graph, "omar@najd.example.com"));
        Assert.Empty(await Find(graph, "gone@example.com"));
    }

    [Fact]
    public async Task AtMostTwentyCustomersAreReturnedOrderedByNumber()
    {
        string database = CustomersTestContext.NewDatabaseName();
        Customer[] customers = [.. Enumerable.Range(1, 21).Select(n =>
        {
            Customer customer = CustomerFixtures.Individual(n, id: new CustomerId(Guid.NewGuid()));
            customer.AddSimpleContact("Shared", "shared@example.com");
            return customer;
        })];
        await CustomersTestContext.Seed(database, customers);
        await using CustomersDbContext context = CustomersTestContext.Create(database, CurrentUsers.Agent(Scopes.AgentA1()));
        var handler = new FindContactsByEmailQueryHandler(CustomersTestContext.Repo(context));

        Result<IReadOnlyList<CustomerContactMatch>> result = await handler.Handle(new FindContactsByEmailQuery("shared@example.com"), Stubs.Ct);

        Assert.Equal(20, result.Value.Count);
        Assert.Equal([.. Enumerable.Range(1, 20).Select(n => $"C-{n:D6}")], result.Value.Select(m => m.CustomerNumber));
    }

    [Fact]
    public async Task TheSpecificationIsTheEmailSpecification()
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        repository.Lists();
        var handler = new FindContactsByEmailQueryHandler(repository);

        await handler.Handle(new FindContactsByEmailQuery("a@b.co"), Stubs.Ct);

        await repository.Received(1).ListAsync(
            Arg.Is<Specification<Customer>>(s => s is Crm.Modules.Customers.Application.Specifications.CustomersWithContactEmailSpecification),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheQueryRequiresTheCustomersReadPermission() =>
        Assert.Equal("customers.read", Stubs.PermissionOf(typeof(FindContactsByEmailQuery)));
}

public sealed class FindContactsByEmailQueryValidatorTests
{
    private readonly FindContactsByEmailQueryValidator _validator = new();

    private ValidationResult Validate(string email) => _validator.Validate(new FindContactsByEmailQuery(email));

    [Fact]
    public void AnEmptyEmailIsInvalid() => Expect.Invalid(Validate(""), "Email");

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void TheEmailIsLimitedByLengthOnly(int length, bool valid) => Assert.Equal(valid, Validate(new string('e', length)).IsValid);

    [Fact]
    public void ThereIsNoShapeRule() => Expect.Valid(Validate("not-an-email"));
}

public sealed class FindPortalContactsByEmailQueryHandlerTests
{
    private static async Task<IReadOnlyList<PortalContactMatch>> Find(SeedGraph graph, string email, OrganisationScope scope)
    {
        await using CustomersDbContext context = graph.Context(scope);
        var handler = new FindPortalContactsByEmailQueryHandler(CustomersTestContext.Repo(context));

        Result<IReadOnlyList<PortalContactMatch>> result = await handler.Handle(new FindPortalContactsByEmailQuery(email), Stubs.Ct);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("")]
    [InlineData("a@b")]
    public async Task AMalformedEmailIsAnEmptySuccessAndTheRepositoryIsNotQueried(string email)
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        var handler = new FindPortalContactsByEmailQueryHandler(repository);

        Result<IReadOnlyList<PortalContactMatch>> result = await handler.Handle(new FindPortalContactsByEmailQuery(email), Stubs.Ct);

        Assert.Empty(result.Value);
        await repository.DidNotReceive().ListAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheScopeIsLiftedSoACallerOfAnotherTenantSeesEveryTenantsPortalContacts()
    {
        SeedGraph graph = await SeedGraph.Create();

        IReadOnlyList<PortalContactMatch> matches = await Find(graph, "ahmed@example.com", Scopes.AgentB1());

        Assert.Equal(["C-000001", "C-000005"], matches.Select(m => graph.NumberOf(m.CustomerId)));
        Assert.Equal([Ids.TenantA.Value, Ids.TenantB.Value], matches.Select(m => m.TenantId));
    }

    [Fact]
    public async Task AnAnonymousCallerWithNoScopeAtAllStillGetsTheMatches()
    {
        SeedGraph graph = await SeedGraph.Create();

        IReadOnlyList<PortalContactMatch> matches = await Find(graph, "ahmed@example.com", OrganisationScope.None);

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public async Task AMatchCarriesWhatSignInNeeds()
    {
        SeedGraph graph = await SeedGraph.Create();

        PortalContactMatch match = (await Find(graph, " AHMED@Example.com ", Scopes.AgentA1()))[0];

        Assert.Equal(graph.Ahmed.Id.Value, match.CustomerId);
        Assert.Equal(Guid.Parse("c2000000-0000-0000-0000-000000000001"), match.ContactId);
        Assert.Equal("Ahmed", match.ContactName);
        Assert.Equal("ahmed@example.com", match.Email);
        Assert.True(match.IsPrimary);
        Assert.Equal("ar", match.PreferredLanguage);
        Assert.Equal(Ids.TenantA.Value, match.TenantId);
        Assert.Equal(Ids.BranchA1.Value, match.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, match.DepartmentId);
    }

    [Fact]
    public async Task OnlyContactsWithPortalAccessMatch()
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Empty(await Find(graph, "sara@najd.example.com", Scopes.AgentA1()));
    }

    [Fact]
    public async Task OnlyActiveCustomersMatch()
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Empty(await Find(graph, "fahad@redsea.example.com", Scopes.AgentA1AndA2()));
    }

    [Fact]
    public async Task ASoftDeletedCustomerStaysHiddenBecauseOnlyTheScopeFilterIsLifted()
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Empty(await Find(graph, "gone@example.com", OrganisationScope.None));
    }

    [Fact]
    public void TheQueryIsAnonymousAndTheSpecificationLiftsOnlyTheScope()
    {
        Assert.True(Stubs.HasAttribute<Crm.BuildingBlocks.Application.Authorization.AllowAnonymousRequestAttribute>(typeof(FindPortalContactsByEmailQuery)));
        Assert.Null(Stubs.PermissionOf(typeof(FindPortalContactsByEmailQuery)));
    }
}

public sealed class FindPortalContactsByEmailQueryValidatorTests
{
    private readonly FindPortalContactsByEmailQueryValidator _validator = new();

    private ValidationResult Validate(string email) => _validator.Validate(new FindPortalContactsByEmailQuery(email));

    [Fact]
    public void AnEmptyEmailIsInvalid() => Expect.Invalid(Validate(""), "Email");

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void TheEmailIsLimitedByLengthOnly(int length, bool valid) => Assert.Equal(valid, Validate(new string('e', length)).IsValid);

    [Fact]
    public void ThereIsNoShapeRule() => Expect.Valid(Validate("not-an-email"));
}

public sealed class GetPortalContactQueryHandlerTests
{
    private static readonly Guid AhmedContact = Guid.Parse("c2000000-0000-0000-0000-000000000001");

    private static async Task<Result<PortalContactMatch>> Get(SeedGraph graph, Guid contact, OrganisationScope scope)
    {
        await using CustomersDbContext context = graph.Context(scope);
        var handler = new GetPortalContactQueryHandler(CustomersTestContext.Repo(context));

        return await handler.Handle(new GetPortalContactQuery(contact), Stubs.Ct);
    }

    [Fact]
    public async Task APortalContactIsFoundWithItsPrimaryFlag()
    {
        SeedGraph graph = await SeedGraph.Create();

        Result<PortalContactMatch> result = await Get(graph, AhmedContact, Scopes.AgentA1());

        Assert.True(result.IsSuccess);
        Assert.Equal(AhmedContact, result.Value.ContactId);
        Assert.True(result.Value.IsPrimary);
        Assert.Equal("ahmed@example.com", result.Value.Email);
    }

    [Fact]
    public async Task TheScopeIsLiftedSoAnotherTenantsContactIsFound()
    {
        SeedGraph graph = await SeedGraph.Create();

        Result<PortalContactMatch> result = await Get(graph, AhmedContact, Scopes.AgentB1());

        Assert.True(result.IsSuccess);
        Assert.Equal(Ids.TenantA.Value, result.Value.TenantId);
        Assert.True((await Get(graph, AhmedContact, OrganisationScope.None)).IsSuccess);
    }

    [Fact]
    public async Task AContactWithoutPortalAccessIsNotFound()
    {
        SeedGraph graph = await SeedGraph.Create();
        Guid sara = Guid.Parse("c2000000-0000-0000-0000-000000000002");

        Result<PortalContactMatch> result = await Get(graph, sara, Scopes.AgentA1());

        Expect.Failure(result, "Customers.PortalContactNotFound", ErrorType.NotFound);
        Assert.Contains(sara.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AContactOfAnInactiveCustomerIsNotFound()
    {
        SeedGraph graph = await SeedGraph.Create();

        Expect.Failure(await Get(graph, Guid.Parse("c2000000-0000-0000-0000-000000000004"), Scopes.AgentA1AndA2()), "Customers.PortalContactNotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task AContactOfASoftDeletedCustomerIsNotFound()
    {
        SeedGraph graph = await SeedGraph.Create();

        Expect.Failure(await Get(graph, Guid.Parse("c2000000-0000-0000-0000-000000000006"), OrganisationScope.None), "Customers.PortalContactNotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task AnUnknownContactIsNotFound()
    {
        SeedGraph graph = await SeedGraph.Create();

        Expect.Failure(await Get(graph, Ids.Missing, Scopes.AgentA1()), "Customers.PortalContactNotFound", ErrorType.NotFound);
    }

    [Fact]
    public void TheQueryIsAnonymous() =>
        Assert.True(Stubs.HasAttribute<Crm.BuildingBlocks.Application.Authorization.AllowAnonymousRequestAttribute>(typeof(GetPortalContactQuery)));

    [Fact]
    public void TheValidatorRequiresAContactId()
    {
        var validator = new GetPortalContactQueryValidator();

        Expect.Valid(validator.Validate(new GetPortalContactQuery(AhmedContact)));
        Expect.Invalid(validator.Validate(new GetPortalContactQuery(Guid.Empty)), "ContactId");
    }
}
