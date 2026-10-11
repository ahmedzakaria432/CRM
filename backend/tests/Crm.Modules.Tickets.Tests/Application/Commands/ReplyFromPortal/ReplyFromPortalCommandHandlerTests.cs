using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Commands.ReplyFromPortal;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ReplyFromPortal;

public sealed class ReplyFromPortalCommandHandlerTests
{
    private static ReplyFromPortalCommandHandler Handler(Harness h) =>
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

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReplyFromPortalCommand(Ids.Ticket1, "x"), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.PortalOnly, ErrorType.Unauthorized);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task TheTicketIsLoadedThroughThePortalSpecificationNeverById()
    {
        Ticket ticket = Owned(TicketStatus.Open);
        Harness h = PortalHarness(ticket);

        await Handler(h).Handle(new ReplyFromPortalCommand(ticket.Id.Value, "Hello"), Stubs.Ct);

        await h.Tickets.Received(1).ListAsync(
            Arg.Is<Specification<Ticket>>(s => s is PortalTicketsSpecification && !s.AsNoTracking), Arg.Any<CancellationToken>());
        await h.Tickets.DidNotReceive().GetByIdAsync(Arg.Any<TicketId>(), Arg.Any<CancellationToken>());
        await h.Tickets.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ATicketThatIsNotVisibleIsNotFoundAsIfItDidNotExist()
    {
        Ticket someoneElses = TicketFixtures.InStatus(TicketStatus.Open, customer: Ids.Customer2, contact: Ids.Contact2);
        Harness h = PortalHarness(someoneElses);

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReplyFromPortalCommand(someoneElses.Id.Value, "Hello"), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
        Assert.Empty(someoneElses.Messages);
    }

    [Fact]
    public async Task AnUnknownTicketGivesTheSameAnswerAsAnotherCustomersTicket()
    {
        Harness h = PortalHarness();

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReplyFromPortalCommand(Ids.Ticket1, "Hello"), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task AFinishedTicketIsRefusedNamingThePortalStatusInEnglish(TicketStatus status)
    {
        Ticket ticket = Owned(status);
        Harness h = PortalHarness(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<PortalTicketDto> result = await Handler(h).Handle(new ReplyFromPortalCommand(ticket.Id.Value, "Hello"), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Equal("The ticket is closed; that action is not allowed in this status.", result.Error.Description);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task TheRefusalNamesThePortalStatusInArabic()
    {
        Ticket ticket = Owned(TicketStatus.Closed);
        Harness h = PortalHarness(ticket);

        using UiCulture culture = UiCulture.Use("ar");
        Result<PortalTicketDto> result = await Handler(h).Handle(new ReplyFromPortalCommand(ticket.Id.Value, "Hello"), Stubs.Ct);

        Assert.Contains("مغلقة", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReplyIsAnInboundMessageByTheContactWhoAlsoRecordsIt()
    {
        Ticket ticket = Owned(TicketStatus.Open);
        Harness h = PortalHarness(ticket);

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReplyFromPortalCommand(ticket.Id.Value, "  Hello  "), Stubs.Ct);

        Expect.Success(result);
        TicketMessage message = Assert.Single(ticket.Messages);
        Assert.Equal(TicketMessageDirection.Inbound, message.Direction);
        Assert.Equal(Ids.Contact1, message.AuthorId);
        Assert.Equal(Ids.Contact1, message.RecordedById);
        Assert.Equal("Hello", message.Body);
        Assert.Equal(1, h.Saves);
        Assert.Equal("InProgress", result.Value.Status);
    }

    [Fact]
    public async Task TheEventIsInboundNotAFirstResponseAndActedByTheContact()
    {
        Ticket ticket = Owned(TicketStatus.Open);
        Harness h = PortalHarness(ticket);

        await Handler(h).Handle(new ReplyFromPortalCommand(ticket.Id.Value, "Hello"), Stubs.Ct);

        var published = Assert.IsType<TicketMessageAddedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal("Inbound", published.Direction);
        Assert.False(published.IsFirstResponse);
        Assert.Equal(Ids.Contact1, published.ActorId);
        Assert.Equal(Ids.Contact1, published.AuthorId);
        Assert.Equal(Ids.Customer1, published.CustomerId);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
        h.AssertOrder("save", "publish");
        h.AssertPublishIsLast();
    }

    [Theory]
    [InlineData(TicketStatus.PendingCustomer)]
    [InlineData(TicketStatus.Resolved)]
    public async Task AReplyThatReopensAlsoPublishesTheStatusChange(TicketStatus from)
    {
        Ticket ticket = Owned(from);
        Harness h = PortalHarness(ticket);

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReplyFromPortalCommand(ticket.Id.Value, "Hello"), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(2, h.Published.Count);
        Assert.IsType<TicketMessageAddedIntegrationEvent>(h.Published[0]);
        var status = Assert.IsType<TicketStatusChangedIntegrationEvent>(h.Published[1]);
        Assert.Equal(from.ToString(), status.FromStatus);
        Assert.Equal("Open", status.ToStatus);
        Assert.Equal(Ids.Contact1, status.ActorId);
        Assert.Equal("InProgress", result.Value.Status);
    }

    [Fact]
    public async Task AnOpenTicketPublishesOneEvent()
    {
        Ticket ticket = Owned(TicketStatus.Open);
        Harness h = PortalHarness(ticket);

        await Handler(h).Handle(new ReplyFromPortalCommand(ticket.Id.Value, "Hello"), Stubs.Ct);

        Assert.IsType<TicketMessageAddedIntegrationEvent>(Assert.Single(h.Published));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankBodyThrowsBodyRequired(string body)
    {
        Ticket ticket = Owned(TicketStatus.Open);
        Harness h = PortalHarness(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(new ReplyFromPortalCommand(ticket.Id.Value, body), Stubs.Ct), ErrorCodes.BodyRequired);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task ANonAdminContactCannotReplyToAColleaguesTicket()
    {
        Ticket colleagues = TicketFixtures.InStatus(TicketStatus.Open, contact: Ids.Contact2);
        Harness h = PortalHarness(colleagues);

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReplyFromPortalCommand(colleagues.Id.Value, "Hi"), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
    }

    [Fact]
    public async Task AnAccountAdminCanReplyToAColleaguesTicketOfTheCustomer()
    {
        Ticket colleagues = TicketFixtures.InStatus(TicketStatus.Open, contact: Ids.Contact2);
        var h = new Harness(user: CurrentUsers.Portal(Scopes.Grant(admin: true))) { Localization = FakeLocalization.Resources() };
        h.Quietly(() => h.Tickets.Lists(colleagues));

        Result<PortalTicketDto> result = await Handler(h).Handle(new ReplyFromPortalCommand(colleagues.Id.Value, "Hi"), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(Ids.Contact1, Assert.Single(colleagues.Messages).AuthorId);
    }
}
