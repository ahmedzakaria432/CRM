using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Commands.ClaimTicket;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Tickets.Tests.Application.Commands.ClaimTicket;

public sealed class ClaimTicketCommandHandlerTests
{
    private readonly RecordingLogger<ClaimTicketCommandHandler> _logger = new();

    private ClaimTicketCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.UnitOfWork, h.User, h.Identity, h.Assembler(), h.Localization, h.Clock, h.Bus, _logger);

    private static Harness HarnessFor(Ticket ticket)
    {
        var h = new Harness { Localization = FakeLocalization.Resources() };
        h.Holding(ticket);
        return h;
    }

    [Fact]
    public async Task AnUnknownTicketIsNotFound()
    {
        var h = new Harness();

        Result<TicketDto> result = await Handler(h).Handle(new ClaimTicketCommand(Ids.Ticket1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnUnassignedWorkingTicketIsAssignedToTheCallerWithoutARowVersionCall()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);
        h.KnowsStaff(TicketFixtures.Staff(Ids.Actor, "The Actor"));

        Result<TicketDto> result = await Handler(h).Handle(new ClaimTicketCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(Ids.Actor, ticket.AssignedAgentId);
        Assert.Equal(Ids.Actor, result.Value.AssignedAgentId);
        Assert.Equal("The Actor", result.Value.AssignedAgentName);
        h.Tickets.DidNotReceive().SetOriginalRowVersion(Arg.Any<Ticket>(), Arg.Any<byte[]>());
        h.AssertOrder("save", "publish");
        h.AssertPublishIsLast();
    }

    [Fact]
    public async Task TheEventNamesTheCallerAsAssigneeAndActorWithNoPreviousAgent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.New);
        Harness h = HarnessFor(ticket);

        await Handler(h).Handle(new ClaimTicketCommand(ticket.Id.Value), Stubs.Ct);

        var published = Assert.IsType<TicketAssignedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Null(published.PreviousAgentId);
        Assert.Equal(Ids.Actor, published.AssignedAgentId);
        Assert.Equal(Ids.Actor, published.ActorId);
        Assert.Equal(ticket.Id.Value, published.TicketId);
        Assert.Equal("T-000001", published.Number);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
        Assert.NotEqual(Guid.Empty, published.Id);
        Assert.Equal(Clock.Now, published.OccurredOnUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnAlreadyAssignedTicketIsAConflictNamingTheAssigneeEvenWhenItIsTheCaller(bool toTheCaller)
    {
        Guid assignee = toTheCaller ? Ids.Actor : Ids.Agent2;
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(assignee);
        Harness h = HarnessFor(ticket);
        h.KnowsStaff(TicketFixtures.Staff(assignee, "Sara Assignee"));

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(new ClaimTicketCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.AlreadyClaimed, ErrorType.Conflict);
        Assert.Equal("This ticket is already claimed by Sara Assignee.", result.Error.Description);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task TheAssigneeNameFallsBackToTheIdWhenIdentityOmitsTheUser()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent2);
        Harness h = HarnessFor(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(new ClaimTicketCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.AlreadyClaimed, ErrorType.Conflict);
        Assert.Contains(Ids.Agent2.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAssigneeNameFallsBackToTheIdWhenIdentityFails()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent2);
        Harness h = HarnessFor(ticket);
        h.Quietly(() => h.Identity.GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IReadOnlyList<Crm.Modules.Identity.Contracts.StaffUserSummary>>(
                Error.Forbidden("Organisation.OutOfScope", "x")))));

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(new ClaimTicketCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.AlreadyClaimed, ErrorType.Conflict);
        Assert.Contains(Ids.Agent2.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AlreadyClaimedIsDecidedBeforeTheStatusRefusalForAnAssignedClosedTicket()
    {
        Ticket assigned = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent2);
        assigned.Resolve(Ids.Actor, Clock.Now);
        assigned.Close(Ids.Actor, Clock.Now);
        Harness h = HarnessFor(assigned);

        Result<TicketDto> result = await Handler(h).Handle(new ClaimTicketCommand(assigned.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.AlreadyClaimed, ErrorType.Conflict);
    }

    [Fact]
    public async Task AnUnassignedClosedTicketIsADomainFailure()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Closed);
        Harness h = HarnessFor(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(new ClaimTicketCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Contains("Closed", result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task ALostRaceIsAConflictNamingTheWinnerWithOneInformationLogAndNoEvent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);
        h.KnowsStaff(TicketFixtures.Staff(Ids.Agent2, "The Winner"));
        Ticket winnerView = TicketFixtures.InStatus(TicketStatus.Open, id: ticket.Id).Assigned(Ids.Agent2);
        h.Quietly(() =>
        {
            h.Tickets.FindsFirstOf(winnerView);
            h.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromException<int>(new ConcurrencyConflictException(new InvalidOperationException("clash"))));
        });

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(new ClaimTicketCommand(ticket.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.AlreadyClaimed, ErrorType.Conflict);
        Assert.Equal("This ticket is already claimed by The Winner.", result.Error.Description);
        Assert.Empty(h.Published);
        LogEntry entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal($"Claim of ticket T-000001 lost the race to {Ids.Agent2}", entry.Message);
    }

    [Fact]
    public async Task ALostRaceWhoseReReadShowsNoAssigneeRethrowsTheException()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);
        Ticket unassignedView = TicketFixtures.InStatus(TicketStatus.Open, id: ticket.Id);
        h.Quietly(() =>
        {
            h.Tickets.FindsFirstOf(unassignedView);
            h.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromException<int>(new ConcurrencyConflictException(new InvalidOperationException("clash"))));
        });

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Handler(h).Handle(new ClaimTicketCommand(ticket.Id.Value), Stubs.Ct));

        Assert.Empty(h.Published);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task ALostRaceWhoseReReadFindsNothingRethrowsTheException()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);
        h.Quietly(() =>
        {
            h.Tickets.FirstOrDefaultAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<Ticket?>(null));
            h.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromException<int>(new ConcurrencyConflictException(new InvalidOperationException("clash"))));
        });

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Handler(h).Handle(new ClaimTicketCommand(ticket.Id.Value), Stubs.Ct));

        Assert.Empty(h.Published);
        Assert.Empty(_logger.Entries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AnUnidentifiableUserThrowsActorRequired(string? userId)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        var h = new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), userId));
        h.Holding(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(new ClaimTicketCommand(ticket.Id.Value), Stubs.Ct), ErrorCodes.ActorRequired);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }
}
