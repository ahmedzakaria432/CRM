using Crm.Modules.Tickets.Application.Commands.UpdateCategory;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.UpdateCategory;

public sealed class UpdateCategoryCommandHandlerTests
{
    private static UpdateCategoryCommandHandler Handler(Harness h) => new(h.Categories, h.UnitOfWork);

    private static UpdateCategoryCommand Command(Category category, string ar = "جديد", string en = "Renamed", int order = 55, bool visible = false) =>
        new(category.Id.Value, ar, en, order, visible, Ids.RowVersion());

    [Fact]
    public async Task AnUnknownCategoryIsNotFound()
    {
        var h = new Harness();

        Result<TicketCategoryDto> result = await Handler(h).Handle(
            new UpdateCategoryCommand(Ids.Category1, "ع", "e", 1, true, Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.CategoryNotFound, ErrorType.NotFound);
        Assert.Contains(Ids.Category1.ToString(), result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task TheRowVersionIsSetBeforeTheChange()
    {
        Category category = TicketFixtures.NewCategory();
        Harness h = new Harness().Holding(category);

        await Handler(h).Handle(Command(category), Stubs.Ct);

        h.Categories.Received(1).SetOriginalRowVersion(category, Arg.Is<byte[]>(v => v.SequenceEqual(Ids.RowVersion())));
        h.AssertOrder("rowversion", "save");
    }

    [Fact]
    public async Task TheFieldsAreReplacedAndTheDtoReturned()
    {
        Category category = TicketFixtures.NewCategory(visible: true);
        Harness h = new Harness().Holding(category);

        Result<TicketCategoryDto> result = await Handler(h).Handle(Command(category), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal("جديد", category.Name.Arabic);
        Assert.Equal("Renamed", category.Name.English);
        Assert.Equal(55, category.DisplayOrder);
        Assert.False(category.IsCustomerVisible);
        Assert.Equal("Renamed", result.Value.Name.En);
        Assert.Equal(55, result.Value.DisplayOrder);
        Assert.False(result.Value.IsCustomerVisible);
        Assert.Equal(Ids.DepartmentA1.Value, result.Value.DepartmentId);
        Assert.Equal(1, h.Saves);
    }

    [Theory]
    [InlineData("", "Renamed")]
    [InlineData("جديد", " ")]
    public async Task ABlankNameSideThrowsCategoryNameRequiredAndSavesNothing(string ar, string en)
    {
        Category category = TicketFixtures.NewCategory();
        Harness h = new Harness().Holding(category);

        await Expect.DomainCode(() => Handler(h).Handle(Command(category, ar, en), Stubs.Ct), ErrorCodes.CategoryNameRequired);

        Assert.Equal(0, h.Saves);
        Assert.Equal("General enquiry", category.Name.English);
    }

    [Fact]
    public async Task AnOutOfRangeOrderThrowsAndSavesNothing()
    {
        Category category = TicketFixtures.NewCategory();
        Harness h = new Harness().Holding(category);

        await Expect.DomainCode(() => Handler(h).Handle(Command(category, order: 10_000), Stubs.Ct), ErrorCodes.CategoryDisplayOrderInvalid);

        Assert.Equal(0, h.Saves);
    }
}
