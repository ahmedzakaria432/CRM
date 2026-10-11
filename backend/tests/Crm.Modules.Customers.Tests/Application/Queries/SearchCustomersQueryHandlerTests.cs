using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Paging;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Queries.SearchCustomers;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;
using FluentValidation.Results;

namespace Crm.Modules.Customers.Tests.Application.Queries;

public sealed class SearchCustomersQueryHandlerTests
{
    // EF InMemory cannot translate the specifications OrderBy + ThenBy pair (Follow-up), so the specification is evaluated
    // in memory over the customers the real scope and soft-delete filters let the caller read.
    private static IRepository<Customer, CustomerId> RepositoryOver(Customer[] visible)
    {
        var repository = Substitute.For<IRepository<Customer, CustomerId>>();
        repository.Lists(visible);
        repository.Counts(visible);
        return repository;
    }

    private static async Task<PagedResult<CustomerListItemDto>> Search(
        SeedGraph graph, SearchCustomersQuery query, OrganisationScope? scope = null)
    {
        Customer[] visible = await graph.Visible(scope ?? Scopes.AgentA1AndA2());
        var handler = new SearchCustomersQueryHandler(RepositoryOver(visible));

        Result<PagedResult<CustomerListItemDto>> result = await handler.Handle(query, Stubs.Ct);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static SearchCustomersQuery Query(
        string? search = null, string? status = null, string? type = null, Guid? department = null, Guid? branch = null,
        string? sort = null, bool descending = true, int page = 1, int pageSize = 20) =>
        new(search, status, type, department, branch, sort, descending, page, pageSize);

    private static string[] Numbers(PagedResult<CustomerListItemDto> page) => [.. page.Items.Select(i => i.Number)];

    [Fact]
    public async Task WithoutFiltersTheCallersCustomersAreListedNewestFirst()
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query());

        Assert.Equal(["C-000004", "C-000003", "C-000002", "C-000001"], Numbers(page));
        Assert.Equal(4, page.Total);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Theory]
    [InlineData("أحمد", "C-000001")]
    [InlineData("Qahtani", "C-000001")]
    [InlineData("Najd", "C-000002")]
    [InlineData("C-000004", "C-000004")]
    [InlineData("Company Ltd", "C-000002")]
    [InlineData("sara@najd", "C-000002")]
    [InlineData("ahmed@example", "C-000001")]
    public async Task TheSearchMatchesNamesNumberLegalNameAndContactEmail(string term, string expected)
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(search: term));

        Assert.Equal([expected], Numbers(page));
    }

    [Theory]
    [InlineData("+966 55 111", "C-000002")]
    [InlineData("+96650123", "C-000001")]
    [InlineData("966 (50) 123", "C-000001")]
    public async Task ThePhoneSideOfTheSearchHasItsSeparatorsStripped(string term, string expected)
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(search: term));

        Assert.Equal([expected], Numbers(page));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankSearchAppliesNoFilter(string? term)
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Equal(4, (await Search(graph, Query(search: term))).Total);
    }

    [Fact]
    public async Task ASearchOfOnlySeparatorsSwitchesThePhoneClauseOffButTheNameClauseStillApplies()
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(search: "- ( )"));

        Assert.Empty(page.Items);
    }

    [Theory]
    [InlineData("Active", 3)]
    [InlineData("active", 3)]
    [InlineData("Inactive", 1)]
    [InlineData("INACTIVE", 1)]
    public async Task TheStatusFilterIgnoresCase(string status, int expected)
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Equal(expected, (await Search(graph, Query(status: status))).Total);
    }

    [Theory]
    [InlineData("Individual", 2)]
    [InlineData("organisation", 2)]
    [InlineData("ORGANISATION", 2)]
    public async Task TheTypeFilterIgnoresCase(string type, int expected)
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Equal(expected, (await Search(graph, Query(type: type))).Total);
    }

    [Fact]
    public async Task TheDepartmentAndBranchFiltersNarrowTheList()
    {
        SeedGraph graph = await SeedGraph.Create();

        Assert.Equal(["C-000003"], Numbers(await Search(graph, Query(department: Ids.DepartmentA2.Value))));
        Assert.Equal(["C-000003"], Numbers(await Search(graph, Query(branch: Ids.BranchA2.Value))));
        Assert.Equal(3, (await Search(graph, Query(department: Ids.DepartmentA1.Value))).Total);
    }

    [Fact]
    public async Task FiltersAreCombinedWithAnd()
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(search: "a", status: "Active", type: "Individual", department: Ids.DepartmentA1.Value));

        Assert.Equal(["C-000004", "C-000001"], Numbers(page));
    }

    [Theory]
    [InlineData("createdAt", false, new[] { "C-000001", "C-000002", "C-000003", "C-000004" })]
    [InlineData("CREATEDAT", true, new[] { "C-000004", "C-000003", "C-000002", "C-000001" })]
    [InlineData("number", false, new[] { "C-000001", "C-000002", "C-000003", "C-000004" })]
    [InlineData("Number", true, new[] { "C-000004", "C-000003", "C-000002", "C-000001" })]
    [InlineData("nameEn", false, new[] { "C-000001", "C-000004", "C-000002", "C-000003" })]
    [InlineData("NAMEEN", true, new[] { "C-000003", "C-000002", "C-000004", "C-000001" })]
    public async Task EverySortKeyWorksInBothDirections(string sort, bool descending, string[] expected)
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(sort: sort, descending: descending));

        Assert.Equal(expected, Numbers(page));
    }

    [Fact]
    public async Task TheArabicNameSortOrdersByTheArabicText()
    {
        SeedGraph graph = await SeedGraph.Create();

        string[] ascending = Numbers(await Search(graph, Query(sort: "nameAr", descending: false)));
        string[] descending = Numbers(await Search(graph, Query(sort: "nameAr", descending: true)));

        Assert.Equal(4, ascending.Length);
        Assert.Equal(ascending.Reverse(), descending);
    }

    [Fact]
    public async Task AnUnknownSortKeyFallsBackToTheCreationTime()
    {
        // Pinned: the validator normally refuses the key; a request that skipped it silently sorts by CreatedAt.
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(sort: "price", descending: false));

        Assert.Equal(["C-000001", "C-000002", "C-000003", "C-000004"], Numbers(page));
    }

    [Fact]
    public async Task PagingReturnsTheRequestedPageAndTheTotalIgnoresPaging()
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> second = await Search(graph, Query(page: 2, pageSize: 3, descending: false, sort: "number"));
        PagedResult<CustomerListItemDto> first = await Search(graph, Query(page: 1, pageSize: 3, descending: false, sort: "number"));
        PagedResult<CustomerListItemDto> beyond = await Search(graph, Query(page: 9, pageSize: 3));

        Assert.Equal(["C-000004"], Numbers(second));
        Assert.Equal(["C-000001", "C-000002", "C-000003"], Numbers(first));
        Assert.Equal(4, second.Total);
        Assert.Equal(2, second.Page);
        Assert.Equal(3, second.PageSize);
        Assert.Empty(beyond.Items);
        Assert.Equal(4, beyond.Total);
    }

    [Fact]
    public async Task ARowMapsThePrimaryContactAndTheAuditStamp()
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(sort: "number", descending: false));

        CustomerListItemDto ahmed = page.Items[0];
        Assert.Equal("Ahmed", ahmed.PrimaryContactName);
        Assert.Equal("ahmed@example.com", ahmed.PrimaryEmail);
        Assert.Equal("+966501234567", ahmed.PrimaryPhone);
        Assert.Equal("Individual", ahmed.Type);
        Assert.Equal("Active", ahmed.Status);
        Assert.Equal(Clock.Later(1), ahmed.CreatedAtUtc);
        CustomerListItemDto faisal = page.Items[3];
        Assert.Null(faisal.PrimaryContactName);
        Assert.Null(faisal.PrimaryEmail);
        Assert.Null(faisal.PrimaryPhone);
    }

    [Fact]
    public async Task TheCallerOfDepartmentA1NeverSeesAnotherDepartmentOrTenant()
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(), Scopes.AgentA1());

        Assert.Equal(["C-000004", "C-000002", "C-000001"], Numbers(page));
        Assert.Equal(3, page.Total);
    }

    [Fact]
    public async Task ADepartmentFilterOutsideTheScopeGivesAnEmptyPageNotAnError()
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(department: Ids.DepartmentA2.Value), Scopes.AgentA1());

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task AnotherTenantSeesOnlyItsOwnCustomers()
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(), Scopes.AgentB1());

        Assert.Equal(["C-000005"], Numbers(page));
    }

    [Fact]
    public async Task ASoftDeletedCustomerIsNeverListed()
    {
        SeedGraph graph = await SeedGraph.Create();

        PagedResult<CustomerListItemDto> page = await Search(graph, Query(search: "Deleted"));

        Assert.Empty(page.Items);
    }

    [Theory]
    [InlineData("Archived", null)]
    [InlineData(null, "Banana")]
    public async Task AnInvalidStatusOrTypeThatSkippedTheValidatorIsAnArgumentError(string? status, string? type)
    {
        SeedGraph graph = await SeedGraph.Create();
        var handler = new SearchCustomersQueryHandler(RepositoryOver(await graph.Visible(Scopes.AgentA1())));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(Query(status: status, type: type), Stubs.Ct));
    }

    [Fact]
    public async Task TheCountDoesNotApplyPagingBecauseItsSpecificationHasNone()
    {
        SeedGraph graph = await SeedGraph.Create();
        var handler = new SearchCustomersQueryHandler(RepositoryOver(await graph.Visible(Scopes.AgentA1AndA2())));

        Result<PagedResult<CustomerListItemDto>> result = await handler.Handle(Query(page: 2, pageSize: 1), Stubs.Ct);

        Assert.Single(result.Value.Items);
        Assert.Equal(4, result.Value.Total);
    }
}

public sealed class SearchCustomersQueryValidatorTests
{
    private readonly SearchCustomersQueryValidator _validator = new(FakeLocalization.Keys());

    private ValidationResult Validate(
        string? search = null, string? status = null, string? type = null, string? sort = null, int page = 1, int pageSize = 20) =>
        _validator.Validate(new SearchCustomersQuery(search, status, type, null, null, sort, true, page, pageSize));

    [Fact]
    public void TheDefaultsAreAValidQuery()
    {
        var query = new SearchCustomersQuery(null, null, null, null, null, null);

        Expect.Valid(_validator.Validate(query));
        Assert.True(query.Descending);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void ThePageStartsAtOne(int page, bool valid) => Assert.Equal(valid, Validate(page: page).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void ThePageSizeIsOneToOneHundred(int size, bool valid) => Assert.Equal(valid, Validate(pageSize: size).IsValid);

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void TheSearchIsLimitedToTwoHundredCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(search: new string('s', length)).IsValid);

    [Theory]
    [InlineData(null, true)]
    [InlineData("Active", true)]
    [InlineData("inactive", true)]
    [InlineData("ACTIVE", true)]
    [InlineData("Archived", false)]
    [InlineData("", false)]
    [InlineData("1", false)]
    public void TheStatusIsAnEnumNameOrAbsent(string? status, bool valid) => Assert.Equal(valid, Validate(status: status).IsValid);

    [Theory]
    [InlineData(null, true)]
    [InlineData("Individual", true)]
    [InlineData("organisation", true)]
    [InlineData("Banana", false)]
    [InlineData("", false)]
    [InlineData("1", false)]
    public void TheTypeIsAnEnumNameOrAbsent(string? type, bool valid) => Assert.Equal(valid, Validate(type: type).IsValid);

    [Theory]
    [InlineData(null, true)]
    [InlineData("createdAt", true)]
    [InlineData("NAMEAR", true)]
    [InlineData("price", false)]
    [InlineData("", false)]
    public void TheSortIsAKnownKeyOrAbsent(string? sort, bool valid) => Assert.Equal(valid, Validate(sort: sort).IsValid);

    [Fact]
    public void AnUnknownSortCarriesTheLocalisedMessage() =>
        Assert.Equal("Customers.Validation.SortUnsupported", Assert.Single(Validate(sort: "price").Errors).ErrorMessage);
}
