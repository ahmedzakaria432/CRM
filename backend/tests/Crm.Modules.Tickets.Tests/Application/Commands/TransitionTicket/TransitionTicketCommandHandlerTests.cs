using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Application.Commands.TransitionTicket;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.TransitionTicket;

public sealed class TransitionTicketCommandHandlerTests
{
    private static TransitionTicketCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.UnitOfWork, h.User, h.Assembler(), h.Localization, h.Clock, h.Bus);

    private static TransitionTicketCommand Command(Ticket ticket, string action) =>
        new(ticket.Id.Value, action, Ids.RowVersion());

    [Fact]
    public async Task AnUnknownTicketIsNotFoundWithNoSaveAndNoEvent()
    {
        var h = new Harness();
        var command = new TransitionTicketCommand(Ids.Ticket1, "Open", Ids.RowVersion());

        Result<TicketDto> result = await Handler(h).Handle(command, Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Contains(Ids.Ticket1.ToString(), result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task TheClientRowVersionIsSetBeforeTheChange()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.New);
        Harness h = new Harness().Holding(ticket);

        await Handler(h).Handle(Command(ticket, "Open"), Stubs.Ct);

        h.Tickets.Received(1).SetOriginalRowVersion(ticket, Arg.Is<byte[]>(v => v.SequenceEqual(Ids.RowVersion())));
        h.AssertOrder("rowversion", "save", "publish");
        h.AssertPublishIsLast();
    }

    public static TheoryData<TicketStatus, string, TicketStatus> Legal() => new()
    {
        { TicketStatus.New, "Open", TicketStatus.Open },
        { TicketStatus.PendingCustomer, "open", TicketStatus.Open },
        { TicketStatus.OnHold, "OPEN", TicketStatus.Open },
        { TicketStatus.Open, "AwaitCustomer", TicketStatus.PendingCustomer },
        { TicketStatus.OnHold, "awaitCustomer", TicketStatus.PendingCustomer },
        { TicketStatus.Open, "PutOnHold", TicketStatus.OnHold },
        { TicketStatus.PendingCustomer, "PutOnHold", TicketStatus.OnHold },
        { TicketStatus.New, "Resolve", TicketStatus.Resolved },
        { TicketStatus.Open, "Resolve", TicketStatus.Resolved },
        { TicketStatus.OnHold, "Resolve", TicketStatus.Resolved },
        { TicketStatus.Resolved, "Close", TicketStatus.Closed },
        { TicketStatus.Resolved, "Reopen", TicketStatus.Open },
        { TicketStatus.Closed, "Reopen", TicketStatus.Open },
        { TicketStatus.New, "Cancel", TicketStatus.Cancelled },
        { TicketStatus.OnHold, "cancel", TicketStatus.Cancelled },
    };

    [Theory]
    [MemberData(nameof(Legal))]
    public async Task ALegalActionChangesTheStatusSavesAndPublishesOneEvent(TicketStatus from, string action, TicketStatus to)
    {
        Ticket ticket = TicketFixtures.InStatus(from);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, action), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(to.ToString(), result.Value.Status);
        Assert.Equal(to, ticket.Status);
        Assert.Equal(1, h.Saves);
        var published = Assert.IsType<TicketStatusChangedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal(from.ToString(), published.FromStatus);
        Assert.Equal(to.ToString(), published.ToStatus);
        Assert.Equal(ticket.Id.Value, published.TicketId);
        Assert.Equal("T-000001", published.Number);
        Assert.Equal(Ids.Actor, published.ActorId);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
        Assert.NotEqual(Guid.Empty, published.Id);
        Assert.Equal(Clock.Now, published.OccurredOnUtc);
    }

    [Fact]
    public async Task ResolveAndCloseSetTheDtoTimestampsAndReopenClearsThem()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness(now: Clock.Later(10)).Holding(ticket);

        Result<TicketDto> resolved = await Handler(h).Handle(Command(ticket, "Resolve"), Stubs.Ct);
        Assert.Equal(Clock.Later(10), resolved.Value.ResolvedAtUtc);
        Assert.Null(resolved.Value.ClosedAtUtc);

        Result<TicketDto> closed = await Handler(h).Handle(Command(ticket, "Close"), Stubs.Ct);
        Assert.Equal(Clock.Later(10), closed.Value.ClosedAtUtc);

        Result<TicketDto> reopened = await Handler(h).Handle(Command(ticket, "Reopen"), Stubs.Ct);
        Assert.Null(reopened.Value.ResolvedAtUtc);
        Assert.Null(reopened.Value.ClosedAtUtc);
    }

    [Fact]
    public async Task AnIllegalPairIsADomainFailureWithTheEnglishStatusNameAndNoSaveOrEvent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Closed);
        Harness h = new Harness { Localization = FakeLocalization.Resources() }.Holding(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, "Cancel"), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Equal("The ticket is Closed; that action is not allowed in this status.", result.Error.Description);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public async Task TheRefusalDetailNamesTheStatusInArabic()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Closed);
        Harness h = new Harness { Localization = FakeLocalization.Resources() }.Holding(ticket);

        using UiCulture culture = UiCulture.Use("ar");
        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, "Cancel"), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Contains("مغلقة", result.Error.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Assign")]
    [InlineData("Claim")]
    [InlineData("Reply")]
    [InlineData("AddNote")]
    public async Task ANonTransitionActionTheStatusAllowsHitsTheDefaultBranchAndNamesTheAction(string action)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, action), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Equal(action, result.Error.Description);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnUnknownActionThatBypassedTheValidatorThrowsArgumentException()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        await Assert.ThrowsAsync<ArgumentException>(() => Handler(h).Handle(Command(ticket, "Explode"), Stubs.Ct));

        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task ANumericActionParsesBecauseEnumParseAcceptsNumbers()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = new Harness().Holding(ticket);

        // 1 is AwaitCustomer.
        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, "1"), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal("PendingCustomer", result.Value.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AnUnidentifiableUserThrowsActorRequiredAndSavesNothing(string? userId)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.New);
        var h = new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), userId));
        h.Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, "Open"), Stubs.Ct), ErrorCodes.ActorRequired);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task TheDtoCarriesTheAllowedActionsOfTheNewStatus()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Resolved);
        Harness h = new Harness().Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, "Close"), Stubs.Ct);

        Assert.Equal(["Reopen", "AddNote"], result.Value.AllowedActions);
    }
}
