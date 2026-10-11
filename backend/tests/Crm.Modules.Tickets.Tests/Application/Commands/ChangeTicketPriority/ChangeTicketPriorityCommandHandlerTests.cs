using Crm.Modules.Tickets.Application.Commands.ChangeTicketPriority;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ChangeTicketPriority;

public sealed class ChangeTicketPriorityCommandHandlerTests
{
    private static ChangeTicketPriorityCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.UnitOfWork, h.User, h.Assembler(), h.Localization, h.Clock, h.Bus);

    private static ChangeTicketPriorityCommand Command(Ticket ticket, string priority) =>
        new(ticket.Id.Value, priority, Ids.RowVersion());

    [Fact]
    public async Task AnUnknownTicketIsNotFound()
    {
        var h = new Harness();

        Result<TicketDto> result = await Handler(h).Handle(new ChangeTicketPriorityCommand(Ids.Ticket1, "High", Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task TheRowVersionIsSetBeforeTheChange()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Handler(h).Handle(Command(ticket, "High"), Stubs.Ct);

        h.Tickets.Received(1).SetOriginalRowVersion(ticket, Arg.Is<byte[]>(v => v.SequenceEqual(Ids.RowVersion())));
        h.AssertOrder("rowversion", "save", "publish");
        h.AssertPublishIsLast();
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task AFinishedTicketIsRefusedWithALocalisedDetail(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);
        Harness h = new Harness { Localization = FakeLocalization.Resources() }.Holding(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, "High"), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Contains(status.ToString(), result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Theory]
    [InlineData("low", TicketPriority.Low)]
    [InlineData("HIGH", TicketPriority.High)]
    [InlineData("Urgent", TicketPriority.Urgent)]
    public async Task ThePriorityIsParsedCaseInsensitively(string priority, TicketPriority expected)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, priority), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(expected, ticket.Priority);
        Assert.Equal(expected.ToString(), result.Value.Priority);
    }

    [Fact]
    public async Task TheEventCarriesTheOldAndNewPriority()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Handler(h).Handle(Command(ticket, "urgent"), Stubs.Ct);

        var published = Assert.IsType<TicketPriorityChangedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal("Normal", published.FromPriority);
        Assert.Equal("Urgent", published.ToPriority);
        Assert.Equal(Ids.Actor, published.ActorId);
        Assert.Equal(ticket.Id.Value, published.TicketId);
        Assert.Equal("T-000001", published.Number);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
    }

    [Fact]
    public async Task TheSamePriorityThrowsPriorityUnchanged()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, "Normal"), Stubs.Ct), ErrorCodes.PriorityUnchanged);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnUnknownPriorityThatBypassedTheValidatorThrowsArgumentException()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Assert.ThrowsAsync<ArgumentException>(() => Handler(h).Handle(Command(ticket, "Critical"), Stubs.Ct));

        Assert.Equal(0, h.Saves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AnUnidentifiableUserThrowsActorRequired(string? userId)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        var h = new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), userId));
        h.Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, "High"), Stubs.Ct), ErrorCodes.ActorRequired);

        Assert.Equal(0, h.Saves);
    }
}
