using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Tickets.Application.Commands.CreateCategory;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.CreateCategory;

public sealed class CreateCategoryCommandHandlerTests
{
    private static CreateCategoryCommandHandler Handler(Harness h) => new(h.Categories, h.UnitOfWork, h.Organisation);

    private static CreateCategoryCommand Command(
        string ar = "شكوى", string en = "Complaint", int order = 30, bool visible = true, Guid? department = null) =>
        new(department ?? Ids.DepartmentA1.Value, ar, en, order, visible);

    private static Harness HarnessWithDepartment(bool active = true)
    {
        var h = new Harness();
        h.KnowsDepartments(TicketFixtures.Department(Ids.DepartmentA1, active: active));
        return h;
    }

    [Fact]
    public async Task AFailedDepartmentLookupIsPassedThrough()
    {
        var h = new Harness();

        Result<TicketCategoryDto> result = await Handler(h).Handle(Command(), Stubs.Ct);

        Expect.Failure(result, "Organisation.DepartmentNotFound", ErrorType.NotFound);
        Assert.Empty(h.AddedCategories);
        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task AnInactiveDepartmentIsADomainFailure()
    {
        Harness h = HarnessWithDepartment(active: false);

        Result<TicketCategoryDto> result = await Handler(h).Handle(Command(), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.CategoryDepartmentInactive, ErrorType.Domain);
        Assert.Empty(h.AddedCategories);
        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task TheTenantAndDepartmentComeFromTheDepartmentReference()
    {
        var h = new Harness();
        h.KnowsDepartments(TicketFixtures.Department(Ids.DepartmentA2, tenant: Ids.TenantB, branch: Ids.BranchB1));

        Result<TicketCategoryDto> result = await Handler(h).Handle(Command(department: Ids.DepartmentA2.Value), Stubs.Ct);

        Expect.Success(result);
        Category added = Assert.Single(h.AddedCategories);
        Assert.Equal(Ids.TenantB, added.TenantId);
        Assert.Equal(Ids.DepartmentA2, added.DepartmentId);
        Assert.Equal(Ids.DepartmentA2.Value, result.Value.DepartmentId);
    }

    [Fact]
    public async Task ASuccessfulCreateAddsSavesOnceAndReturnsTheDto()
    {
        Harness h = HarnessWithDepartment();

        Result<TicketCategoryDto> result = await Handler(h).Handle(Command("شكوى", "Complaint", 30, true), Stubs.Ct);

        Expect.Success(result);
        Category added = Assert.Single(h.AddedCategories);
        Assert.Equal(1, h.Saves);
        h.AssertOrder("add", "save");
        TicketCategoryDto dto = result.Value;
        Assert.Equal(added.Id.Value, dto.Id);
        Assert.Equal("شكوى", dto.Name.Ar);
        Assert.Equal("Complaint", dto.Name.En);
        Assert.Equal(30, dto.DisplayOrder);
        Assert.True(dto.IsCustomerVisible);
        Assert.True(dto.IsActive);
        Assert.Empty(dto.RowVersion);
    }

    [Fact]
    public async Task AHiddenCategoryIsNotCustomerVisible()
    {
        Harness h = HarnessWithDepartment();

        Result<TicketCategoryDto> result = await Handler(h).Handle(Command(visible: false), Stubs.Ct);

        Assert.False(result.Value.IsCustomerVisible);
    }

    [Theory]
    [InlineData("", "Complaint")]
    [InlineData("شكوى", "  ")]
    public async Task ABlankNameSideThrowsCategoryNameRequired(string ar, string en)
    {
        Harness h = HarnessWithDepartment();

        await Expect.DomainCode(() => Handler(h).Handle(Command(ar, en), Stubs.Ct), ErrorCodes.CategoryNameRequired);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.AddedCategories);
    }

    [Fact]
    public async Task ADisplayOrderOfTenThousandThrowsCategoryDisplayOrderInvalid()
    {
        Harness h = HarnessWithDepartment();

        await Expect.DomainCode(() => Handler(h).Handle(Command(order: 10_000), Stubs.Ct), ErrorCodes.CategoryDisplayOrderInvalid);

        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public void TheHandlerHasNoEventBus()
    {
        Type[] parameters = [.. typeof(CreateCategoryCommandHandler).GetConstructors().Single().GetParameters().Select(p => p.ParameterType)];

        Assert.Equal(
            [typeof(Crm.BuildingBlocks.Application.Abstractions.IRepository<Category, CategoryId>), typeof(ITicketsUnitOfWork), typeof(IOrganisationModuleApi)],
            parameters);
    }
}
