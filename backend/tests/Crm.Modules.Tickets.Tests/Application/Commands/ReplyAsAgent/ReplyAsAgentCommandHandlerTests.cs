using Crm.Modules.Tickets.Application.Commands.ReplyAsAgent;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ReplyAsAgent;

public sealed class ReplyAsAgentCommandHandlerTests
{
    private static ReplyAsAgentCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.UnitOfWork, h.User, h.Assembler(), h.Localization, h.Clock, h.Bus);

    private static ReplyAsAgentCommand Command(Ticket ticket, string body = "We are on it.") => new(ticket.Id.Value, body);

    [Fact]
    public async Task AnUnknownTicketIsNotFound()
    {
        var h = new Harness();

        Result<TicketDto> result = await Handler(h).Handle(new ReplyAsAgentCommand(Ids.Ticket1, "x"), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task NoRowVersionIsSet()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Handler(h).Handle(Command(ticket), Stubs.Ct);

        h.Tickets.DidNotReceive().SetOriginalRowVersion(Arg.Any<Ticket>(), Arg.Any<byte[]>());
        h.AssertOrder("save", "publish");
        h.AssertPublishIsLast();
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task AFinishedTicketIsRefusedWithALocalisedDetail(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);
        Harness h = new Harness { Localization = FakeLocalization.Resources() }.Holding(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Contains(status.ToString(), result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
        Assert.Empty(ticket.Messages);
    }

    [Fact]
    public async Task AReplyAppendsAnOutboundMessageAndSavesOnce()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, "  Hello  "), Stubs.Ct);

        Expect.Success(result);
        TicketMessage message = Assert.Single(ticket.Messages);
        Assert.Equal(TicketMessageDirection.Outbound, message.Direction);
        Assert.Equal(Ids.Actor, message.AuthorId);
        Assert.Equal("Hello", message.Body);
        Assert.Equal(Clock.Now, message.OccurredAtUtc);
        Assert.Equal(1, h.Saves);
        Assert.Equal(Clock.Now, result.Value.FirstRespondedAtUtc);
    }

    [Fact]
    public async Task TheFirstReplyPublishesAFirstResponseEventWithoutABody()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open, contact: Ids.Contact1);
        Harness h = new Harness().Holding(ticket);

        await Handler(h).Handle(Command(ticket), Stubs.Ct);

        var published = Assert.IsType<TicketMessageAddedIntegrationEvent>(Assert.Single(h.Published));
        Assert.True(published.IsFirstResponse);
        Assert.Equal("Outbound", published.Direction);
        Assert.Equal(Ids.Actor, published.AuthorId);
        Assert.Equal(Ids.Customer1, published.CustomerId);
        Assert.Equal(Ids.Contact1, published.ContactId);
        Assert.Equal(Assert.Single(ticket.Messages).Id, published.MessageId);
        Assert.Equal(Ids.Actor, published.ActorId);
        Assert.Equal(ticket.Id.Value, published.TicketId);
        Assert.Equal("T-000001", published.Number);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
        Assert.Equal(Clock.Now, published.OccurredOnUtc);
        Assert.Null(typeof(TicketMessageAddedIntegrationEvent).GetProperty("Body"));
    }

    [Fact]
    public async Task ALaterReplyIsNotAFirstResponse()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        ticket.ReplyAsAgent("earlier", Ids.Agent1, Clock.Now);
        Harness h = new Harness(now: Clock.Later(5)).Holding(ticket);

        await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Assert.False(Assert.IsType<TicketMessageAddedIntegrationEvent>(Assert.Single(h.Published)).IsFirstResponse);
        Assert.Equal(Clock.Now, ticket.FirstRespondedAtUtc);
    }

    [Fact]
    public async Task AReplyToAResolvedTicketPublishesTheMessageThenTheStatusChange()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Resolved);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal("Open", result.Value.Status);
        Assert.Equal(2, h.Published.Count);
        Assert.IsType<TicketMessageAddedIntegrationEvent>(h.Published[0]);
        var status = Assert.IsType<TicketStatusChangedIntegrationEvent>(h.Published[1]);
        Assert.Equal("Resolved", status.FromStatus);
        Assert.Equal("Open", status.ToStatus);
        Assert.Equal(Ids.Actor, status.ActorId);
        h.AssertOrder("save", "publish", "publish");
        h.AssertPublishIsLast();
    }

    [Fact]
    public async Task AReplyOnANewTicketPublishesOneEventAndLeavesTheStatus()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.New);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Assert.Equal("New", result.Value.Status);
        Assert.IsType<TicketMessageAddedIntegrationEvent>(Assert.Single(h.Published));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankBodyThrowsBodyRequiredAndSavesNothing(string body)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, body), Stubs.Ct), ErrorCodes.BodyRequired);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task ATooLongBodyThrowsBodyTooLongAndSavesNothing()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, new string('b', 10_001)), Stubs.Ct), ErrorCodes.BodyTooLong);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AnUnidentifiableUserThrowsActorRequired(string? userId)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        var h = new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), userId));
        h.Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket), Stubs.Ct), ErrorCodes.ActorRequired);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }
}
