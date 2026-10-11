using Crm.Modules.Tickets.Application.Commands.ReceiveCustomerReply;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ReceiveCustomerReply;

public sealed class ReceiveCustomerReplyCommandHandlerTests
{
    private static ReceiveCustomerReplyCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.UnitOfWork, h.User, h.Assembler(), h.Localization, h.Clock, h.Bus);

    private static ReceiveCustomerReplyCommand Command(Ticket ticket, string body = "It still fails.") => new(ticket.Id.Value, body);

    [Fact]
    public async Task AnUnknownTicketIsNotFound()
    {
        var h = new Harness();

        Result<TicketDto> result = await Handler(h).Handle(new ReceiveCustomerReplyCommand(Ids.Ticket1, "x"), Stubs.Ct);

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
    }

    [Fact]
    public async Task TheMessageIsInboundAuthoredByTheTicketContactAndRecordedByTheAgent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open, contact: Ids.Contact1);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Expect.Success(result);
        TicketMessage message = Assert.Single(ticket.Messages);
        Assert.Equal(TicketMessageDirection.Inbound, message.Direction);
        Assert.Equal(Ids.Contact1, message.AuthorId);
        Assert.Equal(Ids.Actor, message.RecordedById);
        Assert.Equal(1, h.Saves);
    }

    [Fact]
    public async Task ATicketWithoutAContactGivesTheMessageNoAuthor()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Assert.Null(Assert.Single(ticket.Messages).AuthorId);
        Assert.Null(Assert.IsType<TicketMessageAddedIntegrationEvent>(Assert.Single(h.Published)).AuthorId);
    }

    [Fact]
    public async Task TheEventIsInboundAndNeverAFirstResponse()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open, contact: Ids.Contact1);
        Harness h = new Harness().Holding(ticket);

        await Handler(h).Handle(Command(ticket), Stubs.Ct);

        var published = Assert.IsType<TicketMessageAddedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal("Inbound", published.Direction);
        Assert.False(published.IsFirstResponse);
        Assert.Equal(Ids.Contact1, published.AuthorId);
        Assert.Equal(Ids.Contact1, published.ContactId);
        Assert.Equal(Ids.Customer1, published.CustomerId);
        Assert.Equal(Ids.Actor, published.ActorId);
        Assert.Equal(Assert.Single(ticket.Messages).Id, published.MessageId);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
        Assert.Null(ticket.FirstRespondedAtUtc);
    }

    [Theory]
    [InlineData(TicketStatus.PendingCustomer)]
    [InlineData(TicketStatus.Resolved)]
    public async Task AReplyThatReopensPublishesTheMessageThenTheStatusChange(TicketStatus from)
    {
        Ticket ticket = TicketFixtures.InStatus(from);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal("Open", result.Value.Status);
        Assert.Equal(2, h.Published.Count);
        Assert.IsType<TicketMessageAddedIntegrationEvent>(h.Published[0]);
        var status = Assert.IsType<TicketStatusChangedIntegrationEvent>(h.Published[1]);
        Assert.Equal(from.ToString(), status.FromStatus);
        Assert.Equal("Open", status.ToStatus);
        h.AssertOrder("save", "publish", "publish");
        h.AssertPublishIsLast();
    }

    [Fact]
    public async Task AnOpenTicketPublishesOneEvent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Assert.IsType<TicketMessageAddedIntegrationEvent>(Assert.Single(h.Published));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ABlankBodyThrowsBodyRequired(string body)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, body), Stubs.Ct), ErrorCodes.BodyRequired);

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
    }
}
