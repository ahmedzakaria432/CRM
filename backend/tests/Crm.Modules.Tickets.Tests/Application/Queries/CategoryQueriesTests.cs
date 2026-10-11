using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Queries.GetCategory;
using Crm.Modules.Tickets.Application.Queries.ListCategories;
using Crm.Modules.Tickets.Application.Queries.ListPortalTicketCategories;
using Crm.Modules.Tickets.Application.Queries.ListTicketCategories;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

/// <summary>The four category queries: admin list, agent picker, portal picker and the single admin read.</summary>
public sealed class CategoryQueriesTests
{
    private static Category[] Categories() =>
    [
        TicketFixtures.NewCategory(department: Ids.DepartmentA1, en: "Beta", order: 20),
        TicketFixtures.NewCategory(department: Ids.DepartmentA1, en: "Alpha", order: 20, visible: false),
        TicketFixtures.NewCategory(department: Ids.DepartmentA1, en: "First", order: 10),
        TicketFixtures.NewCategory(department: Ids.DepartmentA1, en: "Old", order: 5, active: false),
        TicketFixtures.NewCategory(department: Ids.DepartmentA2, en: "Elsewhere", order: 1),
    ];

    private static string[] Names(IEnumerable<TicketCategoryDto> dtos) => [.. dtos.Select(d => d.Name.En)];

    // ---- ListCategories (admin) ----

    [Fact]
    public async Task TheAdminListWithNoDepartmentHasNoDepartmentFilterAndShowsEverythingOrdered()
    {
        Category[] categories = Categories();
        var h = new Harness();
        h.Quietly(() => h.Categories.Lists(categories));

        Result<IReadOnlyList<TicketCategoryDto>> result = await new ListCategoriesQueryHandler(h.Categories)
            .Handle(new ListCategoriesQuery(null, false), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(["Elsewhere", "Old", "First", "Alpha", "Beta"], Names(result.Value));
    }

    [Fact]
    public async Task TheAdminListWithADepartmentFiltersToIt()
    {
        var h = new Harness();
        h.Quietly(() => h.Categories.Lists(Categories()));

        Result<IReadOnlyList<TicketCategoryDto>> result = await new ListCategoriesQueryHandler(h.Categories)
            .Handle(new ListCategoriesQuery(Ids.DepartmentA2.Value, false), Stubs.Ct);

        Assert.Equal(["Elsewhere"], Names(result.Value));
    }

    [Fact]
    public async Task TheAdminListCanBeLimitedToActiveCategoriesAndStillShowsHiddenOnes()
    {
        var h = new Harness();
        h.Quietly(() => h.Categories.Lists(Categories()));

        Result<IReadOnlyList<TicketCategoryDto>> result = await new ListCategoriesQueryHandler(h.Categories)
            .Handle(new ListCategoriesQuery(Ids.DepartmentA1.Value, true), Stubs.Ct);

        Assert.Equal(["First", "Alpha", "Beta"], Names(result.Value));
    }

    [Fact]
    public async Task TheAdminListMapsTheDto()
    {
        Category category = TicketFixtures.NewCategory(ar: "تقنية", en: "Technical", order: 7, visible: false);
        var h = new Harness();
        h.Quietly(() => h.Categories.Lists(category));

        Result<IReadOnlyList<TicketCategoryDto>> result = await new ListCategoriesQueryHandler(h.Categories)
            .Handle(new ListCategoriesQuery(null, false), Stubs.Ct);

        TicketCategoryDto dto = Assert.Single(result.Value);
        Assert.Equal(category.Id.Value, dto.Id);
        Assert.Equal(Ids.DepartmentA1.Value, dto.DepartmentId);
        Assert.Equal(("تقنية", "Technical"), (dto.Name.Ar, dto.Name.En));
        Assert.Equal(7, dto.DisplayOrder);
        Assert.False(dto.IsCustomerVisible);
        Assert.True(dto.IsActive);
        Assert.Empty(dto.RowVersion);
    }

    // ---- ListTicketCategories (agent picker) ----

    [Fact]
    public async Task TheAgentPickerListsActiveCategoriesOfEveryDepartmentInScopeWhenNoneIsGiven()
    {
        var h = new Harness(user: CurrentUsers.Agent(Scopes.AgentA1()));
        h.Quietly(() => h.Categories.Lists(Categories()));

        Result<IReadOnlyList<TicketCategoryDto>> result = await new ListTicketCategoriesQueryHandler(h.Categories, h.User)
            .Handle(new ListTicketCategoriesQuery(null), Stubs.Ct);

        Assert.Equal(["First", "Alpha", "Beta"], Names(result.Value));
    }

    [Fact]
    public async Task TheAgentPickerCoversAllDepartmentsOfAMultiDepartmentScope()
    {
        var h = new Harness(user: CurrentUsers.Agent(Scopes.AgentA1AndA2()));
        h.Quietly(() => h.Categories.Lists(Categories()));

        Result<IReadOnlyList<TicketCategoryDto>> result = await new ListTicketCategoriesQueryHandler(h.Categories, h.User)
            .Handle(new ListTicketCategoriesQuery(null), Stubs.Ct);

        Assert.Equal(["Elsewhere", "First", "Alpha", "Beta"], Names(result.Value));
    }

    [Fact]
    public async Task TheAgentPickerGivenDepartmentWinsOverTheScope()
    {
        var h = new Harness(user: CurrentUsers.Agent(Scopes.AgentA1()));
        h.Quietly(() => h.Categories.Lists(Categories()));

        Result<IReadOnlyList<TicketCategoryDto>> result = await new ListTicketCategoriesQueryHandler(h.Categories, h.User)
            .Handle(new ListTicketCategoriesQuery(Ids.DepartmentA2.Value), Stubs.Ct);

        Assert.Equal(["Elsewhere"], Names(result.Value));
    }

    [Fact]
    public async Task TheAgentPickerUsesACategoriesSpecification()
    {
        var h = new Harness();

        await new ListTicketCategoriesQueryHandler(h.Categories, h.User).Handle(new ListTicketCategoriesQuery(null), Stubs.Ct);

        await h.Categories.Received(1).ListAsync(Arg.Is<Specification<Category>>(s => s is CategoriesSpecification && s.AsNoTracking), Arg.Any<CancellationToken>());
    }

    // ---- ListPortalTicketCategories ----

    [Fact]
    public async Task ANonPortalCallerOfThePortalPickerIsUnauthorized()
    {
        var h = new Harness();

        Result<IReadOnlyList<PortalTicketCategoryDto>> result = await new ListPortalTicketCategoriesQueryHandler(h.Categories, h.User)
            .Handle(new ListPortalTicketCategoriesQuery(), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.PortalOnly, ErrorType.Unauthorized);
    }

    [Fact]
    public async Task ThePortalPickerListsActiveCustomerVisibleCategoriesOfTheHomeDepartmentOnly()
    {
        var h = new Harness(user: CurrentUsers.Portal(Scopes.Grant()));
        h.Quietly(() => h.Categories.Lists(Categories()));

        Result<IReadOnlyList<PortalTicketCategoryDto>> result = await new ListPortalTicketCategoriesQueryHandler(h.Categories, h.User)
            .Handle(new ListPortalTicketCategoriesQuery(), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(["First", "Beta"], result.Value.Select(c => c.NameEn));
    }

    [Fact]
    public async Task ThePortalPickerMapsIdAndBothNames()
    {
        Category category = TicketFixtures.NewCategory(ar: "تقنية", en: "Technical");
        var h = new Harness(user: CurrentUsers.Portal(Scopes.Grant()));
        h.Quietly(() => h.Categories.Lists(category));

        Result<IReadOnlyList<PortalTicketCategoryDto>> result = await new ListPortalTicketCategoriesQueryHandler(h.Categories, h.User)
            .Handle(new ListPortalTicketCategoriesQuery(), Stubs.Ct);

        Assert.Equal(new PortalTicketCategoryDto(category.Id.Value, "تقنية", "Technical"), Assert.Single(result.Value));
    }

    [Fact]
    public async Task ThePortalPickerOfAnotherHomeDepartmentSeesThatDepartment()
    {
        var h = new Harness(user: CurrentUsers.Portal(Scopes.Grant(branch: Ids.BranchA2, department: Ids.DepartmentA2)));
        h.Quietly(() => h.Categories.Lists(Categories()));

        Result<IReadOnlyList<PortalTicketCategoryDto>> result = await new ListPortalTicketCategoriesQueryHandler(h.Categories, h.User)
            .Handle(new ListPortalTicketCategoriesQuery(), Stubs.Ct);

        Assert.Equal(["Elsewhere"], result.Value.Select(c => c.NameEn));
    }

    // ---- GetCategory ----

    [Fact]
    public async Task AnUnknownCategoryIsNotFound()
    {
        var h = new Harness();

        Result<TicketCategoryDto> result = await new GetCategoryQueryHandler(h.Categories).Handle(new GetCategoryQuery(Ids.Category1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.CategoryNotFound, ErrorType.NotFound);
        Assert.Contains(Ids.Category1.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFoundCategoryIsMappedWithItsRowVersion()
    {
        Category category = TicketFixtures.NewCategory(ar: "تقنية", en: "Technical", active: false);
        var h = new Harness();
        h.Holding(category);

        Result<TicketCategoryDto> result = await new GetCategoryQueryHandler(h.Categories).Handle(new GetCategoryQuery(category.Id.Value), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(category.Id.Value, result.Value.Id);
        Assert.False(result.Value.IsActive);
        Assert.Same(category.RowVersion, result.Value.RowVersion);
    }
}
