using Crm.Modules.Identity.Contracts;
using Crm.Modules.Tickets.Application.Commands.AssignTicket;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.AssignTicket;

public sealed class AssignTicketCommandHandlerTests
{
    private static AssignTicketCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.UnitOfWork, h.User, h.Organisation, h.Identity, h.Assembler(), h.Localization, h.Clock, h.Bus);

    private static AssignTicketCommand Command(Ticket ticket, Guid agent) => new(ticket.Id.Value, agent, Ids.RowVersion());

    private static Harness HarnessFor(Ticket ticket, bool member = true)
    {
        var h = new Harness { Localization = FakeLocalization.Resources() };
        h.Holding(ticket);
        h.Organisation.IsDepartmentMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(member)));
        return h;
    }

    [Fact]
    public async Task AnUnknownTicketIsNotFound()
    {
        var h = new Harness();

        Result<TicketDto> result = await Handler(h).Handle(new AssignTicketCommand(Ids.Ticket1, Ids.Agent1, Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task TheRowVersionIsSetBeforeTheChange()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);

        await Handler(h).Handle(Command(ticket, Ids.Agent1), Stubs.Ct);

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
        Harness h = HarnessFor(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.Agent1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Contains(status.ToString(), result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
        await h.Organisation.DidNotReceive().IsDepartmentMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnInactiveAssigneeIsADomainFailure()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);
        h.KnowsStaff(new StaffUserSummary(Ids.Agent1, "Gone", false));

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.Agent1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.AssigneeInactive, ErrorType.Domain);
        Assert.Contains(Ids.Agent1.ToString(), result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnIdentityFailureDoesNotBlockTheAssignment()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);
        h.Quietly(() => h.Identity.GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IReadOnlyList<StaffUserSummary>>(Error.Forbidden("Organisation.OutOfScope", "x")))));

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.Agent1), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(Ids.Agent1, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task AnUnknownUserDoesNotBlockTheAssignment()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.Agent1), Stubs.Ct);

        Expect.Success(result);
    }

    [Fact]
    public async Task ANonMemberOfTheTicketsDepartmentIsADomainFailure()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket, member: false);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.Agent1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.AssigneeNotInDepartment, ErrorType.Domain);
        Assert.Equal(0, h.Saves);
        await h.Organisation.Received(1).IsDepartmentMemberAsync(Ids.Agent1, Ids.DepartmentA1.Value, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AFailedMembershipLookupIsTreatedAsNotInDepartment()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);
        h.Organisation.IsDepartmentMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<bool>(Error.NotFound("Organisation.DepartmentNotFound", "x"))));

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.Agent1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.AssigneeNotInDepartment, ErrorType.Domain);
    }

    [Fact]
    public async Task AssigningReplacesAnAssigneeAndPublishesTheEvent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);
        Harness h = HarnessFor(ticket);
        h.KnowsStaff(TicketFixtures.Staff(Ids.Agent2, "Second Agent"));

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.Agent2), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(Ids.Agent2, ticket.AssignedAgentId);
        Assert.Equal("Second Agent", result.Value.AssignedAgentName);
        var published = Assert.IsType<TicketAssignedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal(Ids.Agent1, published.PreviousAgentId);
        Assert.Equal(Ids.Agent2, published.AssignedAgentId);
        Assert.Equal(Ids.Actor, published.ActorId);
        Assert.Equal(ticket.Id.Value, published.TicketId);
        Assert.Equal("T-000001", published.Number);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
    }

    [Fact]
    public async Task AssigningTheCurrentAssigneeAgainThrowsAssigneeUnchanged()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);
        Harness h = HarnessFor(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, Ids.Agent1), Stubs.Ct), ErrorCodes.AssigneeUnchanged);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnEmptyAgentReachingTheHandlerThrowsAssigneeRequired()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, Guid.Empty), Stubs.Ct), ErrorCodes.AssigneeRequired);

        Assert.Equal(0, h.Saves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AnUnidentifiableUserThrowsActorRequired(string? userId)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);
        var other = new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), userId));
        other.Holding(ticket);
        other.Organisation.IsDepartmentMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(true)));

        await Expect.DomainCode(() => Handler(other).Handle(Command(ticket, Ids.Agent1), Stubs.Ct), ErrorCodes.ActorRequired);

        Assert.Equal(0, other.Saves);
        Assert.Empty(other.Published);
        Assert.Equal(0, h.Saves);
    }
}
