using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class CategoryTests
{
    [Fact]
    public void CreateSetsEveryFieldAndIsActiveByDefault()
    {
        Category category = Category.Create(Ids.TenantA, Ids.DepartmentA1, TicketFixtures.Name("شكوى", "Complaint"), 30, true);

        Assert.Equal(Ids.TenantA, category.TenantId);
        Assert.Equal(Ids.DepartmentA1, category.DepartmentId);
        Assert.Equal("شكوى", category.Name.Arabic);
        Assert.Equal("Complaint", category.Name.English);
        Assert.Equal(30, category.DisplayOrder);
        Assert.True(category.IsCustomerVisible);
        Assert.True(category.IsActive);
        Assert.Empty(category.RowVersion);
        Assert.NotEqual(Guid.Empty, category.Id.Value);
    }

    [Fact]
    public void ASuppliedIdWins()
    {
        var id = new CategoryId(Ids.Category1);

        Assert.Equal(id, TicketFixtures.NewCategory(id: id).Id);
    }

    [Fact]
    public void ANullTenantDepartmentOrNameIsRefused()
    {
        Func<TenantId, DepartmentId, LocalizedText, int, bool, CategoryId?, Category> create = Category.Create;
        LocalizedText name = TicketFixtures.Name();

        NullArguments.AssertThrowsArgumentNull(create, null, Ids.DepartmentA1, name, 1, true, null);
        NullArguments.AssertThrowsArgumentNull(create, Ids.TenantA, null, name, 1, true, null);
        NullArguments.AssertThrowsArgumentNull(create, Ids.TenantA, Ids.DepartmentA1, null, 1, true, null);
    }

    [Fact]
    public void ANoneTenantIsRefused()
    {
        Expect.DomainCode(() => TicketFixtures.NewCategory(tenant: TenantId.None), ErrorCodes.CategoryScopeRequired);
    }

    [Fact]
    public void AnEmptyDepartmentIsRefused()
    {
        Expect.DomainCode(() => TicketFixtures.NewCategory(department: new DepartmentId(Guid.Empty)), ErrorCodes.CategoryScopeRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9999)]
    public void ADisplayOrderInRangeIsAccepted(int order)
    {
        Assert.Equal(order, TicketFixtures.NewCategory(order: order).DisplayOrder);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10000)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ADisplayOrderOutOfRangeIsRefused(int order)
    {
        Expect.DomainCode(() => TicketFixtures.NewCategory(order: order), ErrorCodes.CategoryDisplayOrderInvalid);
    }

    [Fact]
    public void TheMaximumDisplayOrderIsNineThousandNineHundredNinetyNine()
    {
        Assert.Equal(9999, Category.DisplayOrderMax);
    }

    [Fact]
    public void UpdateReplacesNameOrderAndVisibilityAndKeepsTheDepartment()
    {
        Category category = TicketFixtures.NewCategory(visible: true);

        category.Update(TicketFixtures.Name("جديد", "Renamed"), 55, false);

        Assert.Equal("جديد", category.Name.Arabic);
        Assert.Equal("Renamed", category.Name.English);
        Assert.Equal(55, category.DisplayOrder);
        Assert.False(category.IsCustomerVisible);
        Assert.Equal(Ids.DepartmentA1, category.DepartmentId);
        Assert.Equal(Ids.TenantA, category.TenantId);
    }

    [Fact]
    public void UpdateWithANullNameIsRefused()
    {
        Category category = TicketFixtures.NewCategory();
        Action<LocalizedText, int, bool> update = category.Update;

        NullArguments.AssertThrowsArgumentNull(update, null, 1, true);
    }

    [Fact]
    public void AFailedUpdateLeavesTheNewNameInPlaceBecauseTheOrderIsCheckedAfterTheAssignment()
    {
        Category category = TicketFixtures.NewCategory(ar: "قديم", en: "Old", order: 10);

        Expect.DomainCode(() => category.Update(TicketFixtures.Name("جديد", "New"), 10000, false), ErrorCodes.CategoryDisplayOrderInvalid);

        // Always-valid gap (follow-up): Name is assigned before the order is validated.
        Assert.Equal("New", category.Name.English);
        Assert.Equal(10, category.DisplayOrder);
        Assert.True(category.IsCustomerVisible);
    }

    [Fact]
    public void DeactivateAndReactivateToggleTheFlag()
    {
        Category category = TicketFixtures.NewCategory();

        category.Deactivate();
        Assert.False(category.IsActive);

        category.Reactivate();
        Assert.True(category.IsActive);
    }

    [Fact]
    public void DeactivatingTwiceIsRefused()
    {
        Category category = TicketFixtures.NewCategory(active: false);

        Expect.DomainCode(category.Deactivate, ErrorCodes.CategoryAlreadyInactive);
    }

    [Fact]
    public void ReactivatingAnActiveCategoryIsRefused()
    {
        Category category = TicketFixtures.NewCategory();

        Expect.DomainCode(category.Reactivate, ErrorCodes.CategoryAlreadyActive);
    }

    [Fact]
    public void ACategoryIsTenantScopedButNotDepartmentScoped()
    {
        Category category = TicketFixtures.NewCategory();

        Assert.IsAssignableFrom<ITenantScoped>(category);
        Assert.False(typeof(IOrganisationScoped).IsAssignableFrom(category.GetType()));
        Assert.IsAssignableFrom<IHasRowVersion>(category);
    }
}
