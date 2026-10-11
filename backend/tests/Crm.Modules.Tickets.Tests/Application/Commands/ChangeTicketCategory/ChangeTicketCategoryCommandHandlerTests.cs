using Crm.Modules.Tickets.Application.Commands.ChangeTicketCategory;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ChangeTicketCategory;

public sealed class ChangeTicketCategoryCommandHandlerTests
{
    private static ChangeTicketCategoryCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.Categories, h.UnitOfWork, h.User, h.Assembler(), h.Localization, h.Clock, h.Bus);

    private static ChangeTicketCategoryCommand Command(Ticket ticket, Category category) =>
        new(ticket.Id.Value, category.Id.Value, Ids.RowVersion());

    [Fact]
    public async Task AnUnknownTicketIsNotFound()
    {
        var h = new Harness();

        Result<TicketDto> result = await Handler(h).Handle(new ChangeTicketCategoryCommand(Ids.Ticket1, Ids.Category1, Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task TheRowVersionIsSetBeforeTheChange()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Category category = TicketFixtures.NewCategory();
        Harness h = new Harness().Holding(ticket).Holding(category);

        await Handler(h).Handle(Command(ticket, category), Stubs.Ct);

        h.Tickets.Received(1).SetOriginalRowVersion(ticket, Arg.Is<byte[]>(v => v.SequenceEqual(Ids.RowVersion())));
        Assert.Equal("rowversion", h.Log.First(e => e is "rowversion" or "save"));
        h.AssertOrder("rowversion", "save", "publish");
        h.AssertPublishIsLast();
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task AFinishedTicketIsRefusedBeforeTheCategoryIsRead(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);
        Category category = TicketFixtures.NewCategory();
        Harness h = new Harness { Localization = FakeLocalization.Resources() }.Holding(ticket).Holding(category);

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, category), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Contains(status.ToString(), result.Error.Description, StringComparison.Ordinal);
        await h.Categories.DidNotReceive().GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task AnUnknownCategoryIsNotFound()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(
            new ChangeTicketCategoryCommand(ticket.Id.Value, Ids.Category1, Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.CategoryNotFound, ErrorType.NotFound);
        Assert.Contains(Ids.Category1.ToString(), result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnInactiveCategoryThrowsCategoryInactive()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Category category = TicketFixtures.NewCategory(active: false);
        Harness h = new Harness().Holding(ticket).Holding(category);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, category), Stubs.Ct), ErrorCodes.CategoryInactive);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task ACategoryOfAnotherDepartmentThrowsCategoryDepartmentMismatch()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Category category = TicketFixtures.NewCategory(department: Ids.DepartmentA2);
        Harness h = new Harness().Holding(ticket).Holding(category);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, category), Stubs.Ct), ErrorCodes.CategoryDepartmentMismatch);

        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task TheCurrentCategoryThrowsCategoryUnchanged()
    {
        Category category = TicketFixtures.NewCategory();
        Ticket ticket = TicketFixtures.Create(category: category);
        Harness h = new Harness().Holding(ticket).Holding(category);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, category), Stubs.Ct), ErrorCodes.CategoryUnchanged);

        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task AnInitialCategoryIsSetAndTheEventHasNoFromCategory()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Category category = TicketFixtures.NewCategory(en: "Technical", ar: "تقنية");
        Harness h = new Harness().Holding(ticket).Holding(category);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, category), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(category.Id.Value, result.Value.Category?.Id);
        Assert.Equal("Technical", result.Value.Category?.Name.En);
        var published = Assert.IsType<TicketCategoryChangedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Null(published.FromCategoryId);
        Assert.Equal(category.Id.Value, published.ToCategoryId);
        Assert.Equal(Ids.Actor, published.ActorId);
        Assert.Equal(ticket.Id.Value, published.TicketId);
        Assert.Equal("T-000001", published.Number);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
    }

    [Fact]
    public async Task ReplacingACategoryCarriesThePreviousOneInTheEvent()
    {
        Category first = TicketFixtures.NewCategory();
        Category second = TicketFixtures.NewCategory(en: "Technical");
        Ticket ticket = TicketFixtures.Create(category: first);
        Harness h = new Harness().Holding(ticket).Holding(second).Holding(first);

        await Handler(h).Handle(Command(ticket, second), Stubs.Ct);

        var published = Assert.IsType<TicketCategoryChangedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal(first.Id.Value, published.FromCategoryId);
        Assert.Equal(second.Id.Value, published.ToCategoryId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AnUnidentifiableUserThrowsActorRequired(string? userId)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Category category = TicketFixtures.NewCategory();
        var h = new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), userId));
        h.Holding(ticket).Holding(category);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, category), Stubs.Ct), ErrorCodes.ActorRequired);

        Assert.Equal(0, h.Saves);
    }
}
