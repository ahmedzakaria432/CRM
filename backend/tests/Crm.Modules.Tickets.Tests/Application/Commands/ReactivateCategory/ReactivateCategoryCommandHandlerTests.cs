using Crm.Modules.Tickets.Application.Commands.ReactivateCategory;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ReactivateCategory;

public sealed class ReactivateCategoryCommandHandlerTests
{
    private static ReactivateCategoryCommandHandler Handler(Harness h) => new(h.Categories, h.UnitOfWork);

    [Fact]
    public async Task AnUnknownCategoryIsNotFound()
    {
        var h = new Harness();

        Result<TicketCategoryDto> result = await Handler(h).Handle(new ReactivateCategoryCommand(Ids.Category1, Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.CategoryNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task TheRowVersionIsSetThenTheCategoryIsReactivated()
    {
        Category category = TicketFixtures.NewCategory(active: false);
        Harness h = new Harness().Holding(category);

        Result<TicketCategoryDto> result = await Handler(h).Handle(new ReactivateCategoryCommand(category.Id.Value, Ids.RowVersion()), Stubs.Ct);

        Expect.Success(result);
        Assert.True(category.IsActive);
        Assert.True(result.Value.IsActive);
        h.Categories.Received(1).SetOriginalRowVersion(category, Arg.Is<byte[]>(v => v.SequenceEqual(Ids.RowVersion())));
        h.AssertOrder("rowversion", "save");
    }

    [Fact]
    public async Task AnAlreadyActiveCategoryThrowsAndSavesNothing()
    {
        Category category = TicketFixtures.NewCategory();
        Harness h = new Harness().Holding(category);

        await Expect.DomainCode(
            () => Handler(h).Handle(new ReactivateCategoryCommand(category.Id.Value, Ids.RowVersion()), Stubs.Ct),
            ErrorCodes.CategoryAlreadyActive);

        Assert.Equal(0, h.Saves);
    }
}
