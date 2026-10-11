using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;

namespace Crm.Modules.Customers.Tests.Application.Specifications;

/// <summary>The specifications are evaluated in memory (criteria, order and paging) over the customers an unrestricted
/// caller can read; EF InMemory cannot run the OrderBy + ThenBy pair, and the SQL translation is not unit-testable.</summary>
public sealed class SpecificationTests
{
    private static async Task<(SeedGraph Graph, Customer[] All)> Graph()
    {
        SeedGraph graph = await SeedGraph.Create();
        return (graph, await graph.Visible(OrganisationScope.Unrestricted));
    }

    private static string[] Numbers(IEnumerable<Customer> customers) => [.. customers.Select(c => c.Number.Value)];

    // ---- CustomerByIdSpecification ----

    [Fact]
    public async Task TheByIdSpecificationMatchesTheIdOnlyAndIsReadOnly()
    {
        (SeedGraph graph, Customer[] all) = await Graph();
        var spec = new CustomerByIdSpecification(graph.Najd.Id);

        Assert.Equal(["C-000002"], Numbers(Stubs.Matching(spec, all)));
        Assert.True(spec.AsNoTracking);
        Assert.Null(spec.Take);
    }

    // ---- CustomersByIdsSpecification ----

    [Fact]
    public async Task TheByIdsSpecificationMatchesAnyOfTheIds()
    {
        (SeedGraph graph, Customer[] all) = await Graph();
        var spec = new CustomersByIdsSpecification([graph.Najd.Id, graph.Faisal.Id, new CustomerId(Ids.Missing)]);

        Assert.Equal(["C-000002", "C-000004"], Numbers(Stubs.Matching(spec, all)).Order());
        Assert.True(spec.AsNoTracking);
    }

    [Fact]
    public async Task TheByIdsSpecificationWithNoIdsMatchesNothing()
    {
        (_, Customer[] all) = await Graph();

        Assert.Empty(Stubs.Matching(new CustomersByIdsSpecification([]), all));
    }

    [Fact]
    public void TheByIdsSpecificationRejectsNull()
    {
        Func<IReadOnlyList<CustomerId>, CustomersByIdsSpecification> create = ids => new CustomersByIdsSpecification(ids);

        NullArguments.AssertThrowsArgumentNull(create, [null]);
    }

    // ---- CustomersWithContactChannelsSpecification ----

    [Fact]
    public async Task TheChannelsSpecificationMatchesAnEmailOrAPhone()
    {
        (_, Customer[] all) = await Graph();

        Assert.Equal(["C-000001", "C-000005"], Numbers(Stubs.Matching(new CustomersWithContactChannelsSpecification(["ahmed@example.com"], []), all)));
        Assert.Equal(["C-000002"], Numbers(Stubs.Matching(new CustomersWithContactChannelsSpecification([], ["+966551112233"]), all)));
        Assert.Equal(
            ["C-000001", "C-000002", "C-000005"],
            Numbers(Stubs.Matching(new CustomersWithContactChannelsSpecification(["ahmed@example.com"], ["+966551112233"]), all)));
    }

    [Fact]
    public async Task TheChannelsSpecificationWithBothListsEmptyMatchesNothing()
    {
        (_, Customer[] all) = await Graph();

        Assert.Empty(Stubs.Matching(new CustomersWithContactChannelsSpecification([], []), all));
    }

    [Fact]
    public void TheChannelsSpecificationIsCappedAtTenOrderedByNumberAndReadOnly()
    {
        var spec = new CustomersWithContactChannelsSpecification(["a@b.co"], []);

        Assert.Equal(10, spec.Take);
        Assert.Equal(0, spec.Skip);
        Assert.NotNull(spec.OrderBy);
        Assert.True(spec.AsNoTracking);
    }

    // ---- CustomersWithContactEmailSpecification ----

    [Fact]
    public async Task TheEmailSpecificationMatchesTheExactNormalisedEmail()
    {
        (_, Customer[] all) = await Graph();

        Assert.Equal(["C-000001", "C-000005"], Numbers(Stubs.Matching(new CustomersWithContactEmailSpecification("ahmed@example.com"), all)));
        Assert.Empty(Stubs.Matching(new CustomersWithContactEmailSpecification("AHMED@example.com"), all));
        Assert.Empty(Stubs.Matching(new CustomersWithContactEmailSpecification("ahmed@example"), all));
    }

    [Fact]
    public void TheEmailSpecificationIsCappedAtTwentyAndReadOnly()
    {
        var spec = new CustomersWithContactEmailSpecification("a@b.co");

        Assert.Equal(20, spec.Take);
        Assert.NotNull(spec.OrderBy);
        Assert.True(spec.AsNoTracking);
    }

    // ---- CustomerLookupSpecification ----

    [Fact]
    public async Task TheLookupSpecificationKeepsActiveCustomersOnly()
    {
        (_, Customer[] all) = await Graph();

        Assert.Equal(["C-000001", "C-000004", "C-000005", "C-000002"], Numbers(Stubs.Matching(new CustomerLookupSpecification(null, 10), all)));
    }

    [Theory]
    [InlineData("Najd", "C-000002")]
    [InlineData("C-000004", "C-000004")]
    [InlineData("فيصل", "C-000004")]
    [InlineData("sara@najd", "C-000002")]
    [InlineData("+966 55 111", "C-000002")]
    public async Task TheLookupSpecificationMatchesNamesNumberEmailAndPhone(string term, string expected)
    {
        (_, Customer[] all) = await Graph();

        Assert.Equal([expected], Numbers(Stubs.Matching(new CustomerLookupSpecification(term, 10), all)));
    }

    [Fact]
    public async Task TheLookupSpecificationTakesAtMostTheRequestedCount()
    {
        (_, Customer[] all) = await Graph();

        Assert.Equal(["C-000001", "C-000004"], Numbers(Stubs.Matching(new CustomerLookupSpecification(null, 2), all)));
        Assert.Equal(2, new CustomerLookupSpecification(null, 2).Take);
        Assert.Equal(0, new CustomerLookupSpecification(null, 2).Skip);
    }

    // ---- CustomerSearchSpecification ----

    [Fact]
    public async Task TheSearchSpecificationAppliesEveryCriterionInOneWhere()
    {
        (SeedGraph graph, Customer[] all) = await Graph();

        var spec = new CustomerSearchSpecification(
            "Najd", CustomerStatus.Active, CustomerType.Organisation, Ids.DepartmentA1.Value, Ids.BranchA1.Value, CustomerSort.Number, false, null, null);

        Assert.Equal(["C-000002"], Numbers(Stubs.Matching(spec, all)));
        Assert.Contains(graph.Najd, Stubs.Matching(spec, all));
    }

    [Fact]
    public async Task TheSearchSpecificationFiltersByStatusTypeDepartmentAndBranchSeparately()
    {
        (_, Customer[] all) = await Graph();

        Assert.Single(Stubs.Matching(Spec(status: CustomerStatus.Inactive), all));
        Assert.Equal(2, Stubs.Matching(Spec(type: CustomerType.Individual), all).Count);
        Assert.Single(Stubs.Matching(Spec(department: Ids.DepartmentA2.Value), all));
        Assert.Single(Stubs.Matching(Spec(branch: Ids.BranchB1.Value), all));
    }

    private static CustomerSearchSpecification Spec(
        string? search = null, CustomerStatus? status = null, CustomerType? type = null, Guid? department = null, Guid? branch = null,
        CustomerSort sort = CustomerSort.Number, bool descending = false, int? page = null, int? pageSize = null) =>
        new(search, status, type, department, branch, sort, descending, page, pageSize);

    [Theory]
    [InlineData(CustomerSort.Number, false, new[] { "C-000001", "C-000002", "C-000003", "C-000004", "C-000005" })]
    [InlineData(CustomerSort.Number, true, new[] { "C-000005", "C-000004", "C-000003", "C-000002", "C-000001" })]
    [InlineData(CustomerSort.NameEn, false, new[] { "C-000001", "C-000004", "C-000005", "C-000002", "C-000003" })]
    [InlineData(CustomerSort.NameEn, true, new[] { "C-000003", "C-000002", "C-000005", "C-000004", "C-000001" })]
    [InlineData(CustomerSort.CreatedAt, false, new[] { "C-000001", "C-000002", "C-000003", "C-000004", "C-000005" })]
    [InlineData(CustomerSort.CreatedAt, true, new[] { "C-000005", "C-000004", "C-000003", "C-000002", "C-000001" })]
    public async Task EachSortKeyOrdersInBothDirections(CustomerSort sort, bool descending, string[] expected)
    {
        (_, Customer[] all) = await Graph();

        Assert.Equal(expected, Numbers(Stubs.Matching(Spec(sort: sort, descending: descending), all)));
    }

    [Fact]
    public async Task TheArabicNameSortIsTheReverseWhenDescending()
    {
        (_, Customer[] all) = await Graph();

        string[] ascending = Numbers(Stubs.Matching(Spec(sort: CustomerSort.NameAr), all));
        string[] descending = Numbers(Stubs.Matching(Spec(sort: CustomerSort.NameAr, descending: true), all));

        Assert.Equal(ascending.Reverse(), descending);
    }

    [Fact]
    public void TheNumberIsTheTieBreakAfterEverySortKey()
    {
        foreach (CustomerSort sort in Enum.GetValues<CustomerSort>())
        {
            Assert.NotNull(Spec(sort: sort).ThenBy);
        }
    }

    [Fact]
    public void AscendingUsesOrderByAndDescendingUsesOrderByDescending()
    {
        Assert.NotNull(Spec(descending: false).OrderBy);
        Assert.Null(Spec(descending: false).OrderByDescending);
        Assert.NotNull(Spec(descending: true).OrderByDescending);
        Assert.Null(Spec(descending: true).OrderBy);
    }

    [Theory]
    [InlineData(1, 10, 0, 10)]
    [InlineData(2, 10, 10, 10)]
    [InlineData(3, 10, 20, 10)]
    [InlineData(5, 1, 4, 1)]
    public void PagingAppliesOnlyWhenBothPageArgumentsAreGiven(int page, int size, int skip, int take)
    {
        CustomerSearchSpecification paged = Spec(page: page, pageSize: size);

        Assert.Equal(skip, paged.Skip);
        Assert.Equal(take, paged.Take);
    }

    [Fact]
    public void ACountSpecificationPassesNoPageAndAHalfGivenPagePagesNothing()
    {
        Assert.Null(Spec().Skip);
        Assert.Null(Spec().Take);
        Assert.Null(Spec(page: 2).Take);
        Assert.Null(Spec(pageSize: 5).Take);
        Assert.True(Spec().AsNoTracking);
    }

    // ---- PortalContactsSpecification ----

    [Fact]
    public void ThePortalSpecificationLiftsOnlyTheScopeFilter()
    {
        var spec = new PortalContactsSpecification("a@b.co", null);

        Assert.True(spec.IgnoreOrganisationScopeFilter);
        Assert.False(spec.IgnoreSoftDeleteFilter);
        Assert.True(spec.AsNoTracking);
        Assert.Equal(20, spec.Take);
        Assert.Equal(0, spec.Skip);
        Assert.NotNull(spec.OrderBy);
    }

    [Fact]
    public async Task ThePortalSpecificationKeepsActiveCustomersWithAPortalContactMatchingTheEmail()
    {
        (_, Customer[] all) = await Graph();

        Assert.Equal(["C-000001", "C-000005"], Numbers(Stubs.Matching(new PortalContactsSpecification("ahmed@example.com", null), all)));
        Assert.Empty(Stubs.Matching(new PortalContactsSpecification("sara@najd.example.com", null), all));
        Assert.Empty(Stubs.Matching(new PortalContactsSpecification("fahad@redsea.example.com", null), all));
    }

    [Fact]
    public async Task ThePortalSpecificationMatchesAContactIdAndEitherArgument()
    {
        (_, Customer[] all) = await Graph();
        var ahmedContact = Guid.Parse("c2000000-0000-0000-0000-000000000001");
        var saraContact = Guid.Parse("c2000000-0000-0000-0000-000000000002");

        Assert.Equal(["C-000001"], Numbers(Stubs.Matching(new PortalContactsSpecification(null, ahmedContact), all)));
        Assert.Empty(Stubs.Matching(new PortalContactsSpecification(null, saraContact), all));
        Assert.Equal(["C-000001", "C-000005"], Numbers(Stubs.Matching(new PortalContactsSpecification("ahmed@example.com", saraContact), all)));
        Assert.Empty(Stubs.Matching(new PortalContactsSpecification(null, null), all));
    }
}

public sealed class CustomerSortParserTests
{
    [Theory]
    [InlineData("createdAt", CustomerSort.CreatedAt)]
    [InlineData("CREATEDAT", CustomerSort.CreatedAt)]
    [InlineData("number", CustomerSort.Number)]
    [InlineData("Number", CustomerSort.Number)]
    [InlineData("nameEn", CustomerSort.NameEn)]
    [InlineData("NAMEEN", CustomerSort.NameEn)]
    [InlineData("nameAr", CustomerSort.NameAr)]
    [InlineData("namear", CustomerSort.NameAr)]
    public void AKnownKeyParsesInAnyCase(string value, CustomerSort expected)
    {
        Assert.True(CustomerSortParser.TryParse(value, out CustomerSort sort));
        Assert.Equal(expected, sort);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("")]
    [InlineData("price")]
    [InlineData(" number")]
    public void AnUnknownKeyFailsAndFallsBackToTheCreationTime(string value)
    {
        Assert.False(CustomerSortParser.TryParse(value, out CustomerSort sort));
        Assert.Equal(CustomerSort.CreatedAt, sort);
    }

    [Fact]
    public void ANullKeyIsAnArgumentError()
    {
        Func<string, bool> parse = value => CustomerSortParser.TryParse(value, out _);

        NullArguments.AssertThrowsArgumentNull(parse, [null]);
    }
}
