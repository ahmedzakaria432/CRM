using Crm.Modules.Tickets.Application.Commands.UnassignTicket;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.UnassignTicket;

public sealed class UnassignTicketCommandHandlerTests
{
    private static UnassignTicketCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.UnitOfWork, h.User, h.Assembler(), h.Localization, h.Clock, h.Bus);

    private static UnassignTicketCommand Command(Ticket ticket) => new(ticket.Id.Value, Ids.RowVersion());

    [Fact]
    public async Task AnUnknownTicketIsNotFound()
    {
        var h = new Harness();

        Result<TicketDto> result = await Handler(h).Handle(new UnassignTicketCommand(Ids.Ticket1, Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnAssignedTicketIsClearedAfterTheRowVersionIsSet()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Expect.Success(result);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(result.Value.AssignedAgentId);
        Assert.Null(result.Value.AssignedAgentName);
        h.Tickets.Received(1).SetOriginalRowVersion(ticket, Arg.Is<byte[]>(v => v.SequenceEqual(Ids.RowVersion())));
        h.AssertOrder("rowversion", "save", "publish");
        h.AssertPublishIsLast();
    }

    [Fact]
    public async Task TheEventCarriesThePreviousAgentAndNoNewOne()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);
        Harness h = new Harness().Holding(ticket);

        await Handler(h).Handle(Command(ticket), Stubs.Ct);

        var published = Assert.IsType<TicketAssignedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal(Ids.Agent1, published.PreviousAgentId);
        Assert.Null(published.AssignedAgentId);
        Assert.Equal(Ids.Actor, published.ActorId);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
        Assert.Equal("T-000001", published.Number);
    }

    [Fact]
    public async Task AnUnassignedTicketThrowsNotAssigned()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket), Stubs.Ct), ErrorCodes.NotAssigned);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AClosedTicketIsARefusalWithALocalisedDetail()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Closed);
        Harness h = new Harness { Localization = FakeLocalization.Resources() }.Holding(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Contains("Closed", result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(0, h.Saves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AnUnidentifiableUserThrowsActorRequired(string? userId)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);
        var h = new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), userId));
        h.Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket), Stubs.Ct), ErrorCodes.ActorRequired);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }
}
