using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Commands.ReopenPortalTicket;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ReopenPortalTicket;

public sealed class ReopenPortalTicketCommandHandlerTests
{
    private static ReopenPortalTicketCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.UnitOfWork, h.User, h.Mapper(), h.Localization, h.Clock, h.Bus);

    private static Harness PortalHarness(params Ticket[] visible)
    {
        var h = new Harness(user: CurrentUsers.Portal(Scopes.Grant())) { Localization = FakeLocalization.Resources() };
        h.Quietly(() => h.Tickets.Lists(visible));
        return h;
    }

    private static Ticket Owned(TicketStatus status) => TicketFixtures.InStatus(status, contact: Ids.Contact1);

    [Fact]
    public async Task ANonPortalCallerIsUnauthorized()
    {
        var h = new Harness();

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReopenPortalTicketCommand(Ids.Ticket1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.PortalOnly, ErrorType.Unauthorized);
        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task ATicketThatIsNotVisibleIsNotFound()
    {
        Ticket someoneElses = TicketFixtures.InStatus(TicketStatus.Resolved, customer: Ids.Customer2, contact: Ids.Contact2);
        Harness h = PortalHarness(someoneElses);

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReopenPortalTicketCommand(someoneElses.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(TicketStatus.Resolved, someoneElses.Status);
    }

    [Fact]
    public async Task TheTicketIsLoadedThroughThePortalSpecificationNeverById()
    {
        Ticket ticket = Owned(TicketStatus.Resolved);
        Harness h = PortalHarness(ticket);

        await Handler(h).Handle(new ReopenPortalTicketCommand(ticket.Id.Value), Stubs.Ct);

        await h.Tickets.Received(1).ListAsync(
            Arg.Is<Specification<Ticket>>(s => s is PortalTicketsSpecification && !s.AsNoTracking), Arg.Any<CancellationToken>());
        await h.Tickets.DidNotReceive().GetByIdAsync(Arg.Any<TicketId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AResolvedTicketIsReopenedAndShownAsInProgress()
    {
        Ticket ticket = Owned(TicketStatus.Resolved);
        Harness h = PortalHarness(ticket);

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReopenPortalTicketCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal("InProgress", result.Value.Status);
        Assert.Equal(["Reply", "RequestClosure"], result.Value.Actions);
        Assert.Null(result.Value.ResolvedAtUtc);
        Assert.Equal(TicketActivityKind.Reopened, ticket.Activities.Last().Kind);
        Assert.Equal(Ids.Contact1, ticket.Activities.Last().ActorId);
        var published = Assert.IsType<TicketStatusChangedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal("Resolved", published.FromStatus);
        Assert.Equal("Open", published.ToStatus);
        Assert.Equal(Ids.Contact1, published.ActorId);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        h.AssertOrder("save", "publish");
        h.AssertPublishIsLast();
    }

    [Fact]
    public async Task AClosedTicketCannotBeReopenedByTheCustomer()
    {
        Ticket ticket = Owned(TicketStatus.Closed);
        Harness h = PortalHarness(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<PortalTicketDto> result = await Handler(h).Handle(new ReopenPortalTicketCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Equal("The ticket is closed; that action is not allowed in this status.", result.Error.Description);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.PendingCustomer)]
    [InlineData(TicketStatus.OnHold)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task EveryOtherStatusIsRefusedWithNoSave(TicketStatus status)
    {
        Ticket ticket = Owned(status);
        Harness h = PortalHarness(ticket);

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReopenPortalTicketCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
        Assert.Equal(status, ticket.Status);
    }
}
