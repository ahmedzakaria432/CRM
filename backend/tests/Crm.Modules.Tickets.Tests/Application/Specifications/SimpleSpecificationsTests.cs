using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Tests.Application.Specifications;

/// <summary>TicketByIdSpecification, CategoriesByIdsSpecification and CategoriesSpecification, evaluated in memory.</summary>
public sealed class SimpleSpecificationsTests
{
    // ---- TicketByIdSpecification ----

    [Fact]
    public void TheByIdSpecificationKeepsOnlyThatTicketAndIsReadOnly()
    {
        Ticket first = TicketFixtures.Create(1);
        Ticket second = TicketFixtures.Create(2);
        var spec = new TicketByIdSpecification(first.Id);

        Assert.Equal([first], Stubs.Matching(spec, first, second));
        Assert.True(spec.AsNoTracking);
        Assert.Null(spec.Skip);
        Assert.Null(spec.Take);
        Assert.Null(spec.OrderBy);
    }

    // ---- CategoriesByIdsSpecification ----

    [Fact]
    public void TheByIdsSpecificationKeepsTheListedCategories()
    {
        Category one = TicketFixtures.NewCategory(en: "One");
        Category two = TicketFixtures.NewCategory(en: "Two");
        Category three = TicketFixtures.NewCategory(en: "Three");
        var spec = new CategoriesByIdsSpecification([one.Id, three.Id]);

        Assert.Equal([one, three], Stubs.Matching(spec, one, two, three));
        Assert.True(spec.AsNoTracking);
    }

    [Fact]
    public void AnEmptyIdListMatchesNothing()
    {
        Category one = TicketFixtures.NewCategory();

        Assert.Empty(Stubs.Matching(new CategoriesByIdsSpecification([]), one));
    }

    [Fact]
    public void ANullIdListIsRefused()
    {
        Func<IReadOnlyList<CategoryId>, CategoriesByIdsSpecification> create = ids => new CategoriesByIdsSpecification(ids);

        NullArguments.AssertThrowsArgumentNull(create, (object?)null);
    }

    // ---- CategoriesSpecification ----

    private static Category[] Set() =>
    [
        TicketFixtures.NewCategory(department: Ids.DepartmentA1, en: "Zeta", order: 10),
        TicketFixtures.NewCategory(department: Ids.DepartmentA1, en: "Alpha", order: 10, visible: false),
        TicketFixtures.NewCategory(department: Ids.DepartmentA1, en: "Early", order: 1),
        TicketFixtures.NewCategory(department: Ids.DepartmentA1, en: "Inactive", order: 5, active: false),
        TicketFixtures.NewCategory(department: Ids.DepartmentA2, en: "Other", order: 3),
    ];

    private static string[] Names(IEnumerable<Category> categories) => [.. categories.Select(c => c.Name.English)];

    [Fact]
    public void ANullDepartmentListKeepsEveryDepartment()
    {
        Assert.Equal(["Early", "Other", "Inactive", "Alpha", "Zeta"], Names(Stubs.Matching(new CategoriesSpecification(null, false, false), Set())));
    }

    [Fact]
    public void ADepartmentListNarrowsToThoseDepartments()
    {
        Assert.Equal(["Other"], Names(Stubs.Matching(new CategoriesSpecification([Ids.DepartmentA2], false, false), Set())));
        Assert.Equal(
            ["Early", "Other", "Inactive", "Alpha", "Zeta"],
            Names(Stubs.Matching(new CategoriesSpecification([Ids.DepartmentA1, Ids.DepartmentA2], false, false), Set())));
    }

    [Fact]
    public void AnEmptyDepartmentListMatchesNothing()
    {
        Assert.Empty(Stubs.Matching(new CategoriesSpecification([], false, false), Set()));
    }

    [Fact]
    public void ActiveOnlyDropsInactiveCategories()
    {
        Assert.Equal(["Early", "Other", "Alpha", "Zeta"], Names(Stubs.Matching(new CategoriesSpecification(null, true, false), Set())));
    }

    [Fact]
    public void CustomerVisibleOnlyDropsHiddenCategories()
    {
        Assert.Equal(["Early", "Other", "Inactive", "Zeta"], Names(Stubs.Matching(new CategoriesSpecification(null, false, true), Set())));
    }

    [Fact]
    public void AllFiltersCombine()
    {
        Assert.Equal(["Early", "Zeta"], Names(Stubs.Matching(new CategoriesSpecification([Ids.DepartmentA1], true, true), Set())));
    }

    [Fact]
    public void CategoriesAreOrderedByDisplayOrderThenEnglishNameAndReadOnly()
    {
        var spec = new CategoriesSpecification(null, false, false);

        Assert.True(spec.AsNoTracking);
        Assert.NotNull(spec.OrderBy);
        Assert.NotNull(spec.ThenBy);
        Assert.Equal(["Alpha", "Zeta"], Names(Stubs.Matching(spec, Set()).Where(c => c.DisplayOrder == 10)));
    }
}
