using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Commands.RequestTicketClosure;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.RequestTicketClosure;

public sealed class RequestTicketClosureCommandHandlerTests
{
    private static RequestTicketClosureCommandHandler Handler(Harness h) =>
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

        Result<PortalTicketDto> result = await Handler(h).Handle(new RequestTicketClosureCommand(Ids.Ticket1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.PortalOnly, ErrorType.Unauthorized);
        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task ATicketThatIsNotVisibleIsNotFoundNeverForbidden()
    {
        Ticket someoneElses = TicketFixtures.InStatus(TicketStatus.Open, customer: Ids.Customer2, contact: Ids.Contact2);
        Harness h = PortalHarness(someoneElses);

        Result<PortalTicketDto> result = await Handler(h).Handle(new RequestTicketClosureCommand(someoneElses.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(TicketStatus.Open, someoneElses.Status);
        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task TheTicketIsLoadedThroughThePortalSpecificationNeverById()
    {
        Ticket ticket = Owned(TicketStatus.Open);
        Harness h = PortalHarness(ticket);

        await Handler(h).Handle(new RequestTicketClosureCommand(ticket.Id.Value), Stubs.Ct);

        await h.Tickets.Received(1).ListAsync(
            Arg.Is<Specification<Ticket>>(s => s is PortalTicketsSpecification && !s.AsNoTracking), Arg.Any<CancellationToken>());
        await h.Tickets.DidNotReceive().GetByIdAsync(Arg.Any<TicketId>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.PendingCustomer)]
    [InlineData(TicketStatus.OnHold)]
    public async Task AWorkingTicketIsResolvedAndTheEventNamesTheContact(TicketStatus from)
    {
        Ticket ticket = Owned(from);
        Harness h = PortalHarness(ticket);

        Result<PortalTicketDto> result = await Handler(h).Handle(new RequestTicketClosureCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal("Resolved", result.Value.Status);
        Assert.Equal(["Reply", "Reopen"], result.Value.Actions);
        Assert.Equal(Clock.Now, result.Value.ResolvedAtUtc);
        var published = Assert.IsType<TicketStatusChangedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal(from.ToString(), published.FromStatus);
        Assert.Equal("Resolved", published.ToStatus);
        Assert.Equal(Ids.Contact1, published.ActorId);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
        h.AssertOrder("save", "publish");
        h.AssertPublishIsLast();
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task AFinishedTicketIsARefusalNamingThePortalStatusWithNoSave(TicketStatus status)
    {
        Ticket ticket = Owned(status);
        Harness h = PortalHarness(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<PortalTicketDto> result = await Handler(h).Handle(new RequestTicketClosureCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        string expectedName = status == TicketStatus.Resolved ? "resolved" : "closed";
        Assert.Equal($"The ticket is {expectedName}; that action is not allowed in this status.", result.Error.Description);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
        Assert.Equal(status, ticket.Status);
    }
}
