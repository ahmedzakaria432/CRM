using System.Reflection;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Tickets.Application.Commands.AddInternalNote;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.AddInternalNote;

public sealed class AddInternalNoteCommandHandlerTests
{
    private static AddInternalNoteCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.UnitOfWork, h.User, h.Assembler(), h.Clock);

    private static AddInternalNoteCommand Command(Ticket ticket, string body = "Check the logs.") => new(ticket.Id.Value, body);

    [Fact]
    public async Task AnUnknownTicketIsNotFound()
    {
        var h = new Harness();

        Result<TicketDto> result = await Handler(h).Handle(new AddInternalNoteCommand(Ids.Ticket1, "x"), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.PendingCustomer)]
    [InlineData(TicketStatus.OnHold)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task ANoteIsAllowedInEveryStatusWithNoRefusalCheck(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(status.ToString(), result.Value.Status);
        TicketNote note = Assert.Single(ticket.Notes);
        Assert.Equal(Ids.Actor, note.AuthorId);
        Assert.Equal("Check the logs.", note.Body);
        Assert.Equal(Clock.Now, note.OccurredAtUtc);
        Assert.Equal(1, h.Saves);
    }

    [Fact]
    public async Task NoRowVersionIsSetAndNothingIsPublished()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Handler(h).Handle(Command(ticket), Stubs.Ct);

        h.Tickets.DidNotReceive().SetOriginalRowVersion(Arg.Any<Ticket>(), Arg.Any<byte[]>());
        Assert.Empty(h.Published);
        h.AssertOrder("save");
    }

    [Fact]
    public void TheHandlerInjectsNeitherTheEventBusNorTheLocalisation()
    {
        Type[] parameters = [.. typeof(AddInternalNoteCommandHandler).GetConstructors().Single().GetParameters().Select(p => p.ParameterType)];

        Assert.DoesNotContain(typeof(IEventBus), parameters);
        Assert.DoesNotContain(typeof(ILocalizationService), parameters);
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
    }

    [Fact]
    public async Task ATooLongBodyThrowsBodyTooLong()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, new string('n', 10_001)), Stubs.Ct), ErrorCodes.BodyTooLong);

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

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket), Stubs.Ct), ErrorCodes.ActorRequired);

        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task TheDtoCarriesNoNoteBecauseNotesAreReadOnlyThroughTheThread()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket), Stubs.Ct);

        Assert.Null(typeof(TicketDto).GetProperty("Notes", BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(2, result.Value.Activities.Count);
    }
}
