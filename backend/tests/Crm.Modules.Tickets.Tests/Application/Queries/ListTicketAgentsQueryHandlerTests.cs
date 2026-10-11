using Crm.Modules.Identity.Contracts;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Tickets.Application.Queries.ListTicketAgents;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class ListTicketAgentsQueryHandlerTests
{
    private readonly Harness _h = new(user: CurrentUsers.Agent(Scopes.AgentA1AndA2()));

    private ListTicketAgentsQueryHandler Handler() => new(_h.User, _h.Organisation, _h.Identity);

    private void Members(params DepartmentMember[] members) =>
        _h.Organisation.GetDepartmentMembersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<DepartmentMember>>(members)));

    private static DepartmentMember Member(Guid user, DepartmentId department) => new(user, department.Value, "Agent");

    [Fact]
    public async Task ADepartmentOutsideTheScopeIsForbiddenAndOrganisationIsNotAsked()
    {
        Result<IReadOnlyList<TicketAgentDto>> result = await Handler().Handle(new ListTicketAgentsQuery(Ids.DepartmentB1.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.OutOfScope, ErrorType.Forbidden);
        await _h.Organisation.DidNotReceive().GetDepartmentMembersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _h.Identity.DidNotReceive().GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ADepartmentInScopeIsTheOnlyOneAskedFor()
    {
        Members();

        await Handler().Handle(new ListTicketAgentsQuery(Ids.DepartmentA2.Value), Stubs.Ct);

        await _h.Organisation.Received(1).GetDepartmentMembersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(Ids.DepartmentA2.Value)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoDepartmentMeansEveryDepartmentOfTheScope()
    {
        Members();

        await Handler().Handle(new ListTicketAgentsQuery(null), Stubs.Ct);

        await _h.Organisation.Received(1).GetDepartmentMembersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(Ids.DepartmentA1.Value) && ids.Contains(Ids.DepartmentA2.Value)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMembersFailureIsPassedThroughAndIdentityIsNotAsked()
    {
        _h.Organisation.GetDepartmentMembersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IReadOnlyList<DepartmentMember>>(Error.NotFound("Organisation.DepartmentNotFound", "x"))));

        Result<IReadOnlyList<TicketAgentDto>> result = await Handler().Handle(new ListTicketAgentsQuery(null), Stubs.Ct);

        Expect.Failure(result, "Organisation.DepartmentNotFound", ErrorType.NotFound);
        await _h.Identity.DidNotReceive().GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnIdentityFailureIsPassedThrough()
    {
        Members(Member(Ids.Agent1, Ids.DepartmentA1));
        _h.Quietly(() => _h.Identity.GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IReadOnlyList<StaffUserSummary>>(Error.Forbidden("Organisation.OutOfScope", "x")))));

        Result<IReadOnlyList<TicketAgentDto>> result = await Handler().Handle(new ListTicketAgentsQuery(null), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.OutOfScope, ErrorType.Forbidden);
    }

    [Fact]
    public async Task InactiveUsersAreDroppedAndDepartmentsAreGroupedPerUserWithoutDuplicates()
    {
        Members(
            Member(Ids.Agent1, Ids.DepartmentA1), Member(Ids.Agent1, Ids.DepartmentA2), Member(Ids.Agent1, Ids.DepartmentA1),
            Member(Ids.Agent2, Ids.DepartmentA1), Member(Ids.Actor, Ids.DepartmentA2));
        _h.KnowsStaff(
            TicketFixtures.Staff(Ids.Agent1, "Agent One"), TicketFixtures.Staff(Ids.Agent2, "Agent Two", active: false),
            TicketFixtures.Staff(Ids.Actor, "Actor"));

        using UiCulture culture = UiCulture.Use("en");
        Result<IReadOnlyList<TicketAgentDto>> result = await Handler().Handle(new ListTicketAgentsQuery(null), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(["Actor", "Agent One"], result.Value.Select(a => a.DisplayName));
        Assert.Equal([Ids.DepartmentA1.Value, Ids.DepartmentA2.Value], result.Value.Single(a => a.Id == Ids.Agent1).DepartmentIds);
        Assert.Equal([Ids.DepartmentA2.Value], result.Value.Single(a => a.Id == Ids.Actor).DepartmentIds);
    }

    [Fact]
    public async Task AgentsAreOrderedByDisplayNameInTheCurrentCulture()
    {
        Members(Member(Ids.Agent1, Ids.DepartmentA1), Member(Ids.Agent2, Ids.DepartmentA1), Member(Ids.Actor, Ids.DepartmentA1));
        _h.KnowsStaff(
            TicketFixtures.Staff(Ids.Agent1, "Charlie"), TicketFixtures.Staff(Ids.Agent2, "alpha"), TicketFixtures.Staff(Ids.Actor, "Bravo"));

        using UiCulture culture = UiCulture.Use("en");
        Result<IReadOnlyList<TicketAgentDto>> result = await Handler().Handle(new ListTicketAgentsQuery(null), Stubs.Ct);

        Assert.Equal(["alpha", "Bravo", "Charlie"], result.Value.Select(a => a.DisplayName));
    }

    [Fact]
    public async Task ArabicNamesAreAllListedUnderTheArabicCulture()
    {
        Members(Member(Ids.Agent1, Ids.DepartmentA1), Member(Ids.Agent2, Ids.DepartmentA1));
        _h.KnowsStaff(TicketFixtures.Staff(Ids.Agent1, "سارة"), TicketFixtures.Staff(Ids.Agent2, "أحمد"));

        using UiCulture culture = UiCulture.Use("ar");
        Result<IReadOnlyList<TicketAgentDto>> result = await Handler().Handle(new ListTicketAgentsQuery(null), Stubs.Ct);

        // The collation of Arabic names is culture and ICU dependent: assert the set, not the order.
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, a => a.DisplayName == "سارة");
        Assert.Contains(result.Value, a => a.DisplayName == "أحمد");
    }

    [Fact]
    public async Task IdentityIsAskedForAtMostTwoHundredIds()
    {
        Members([.. Enumerable.Range(0, 250).Select(_ => Member(Guid.NewGuid(), Ids.DepartmentA1))]);

        await Handler().Handle(new ListTicketAgentsQuery(null), Stubs.Ct);

        await _h.Identity.Received(1).GetStaffUsersAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 200), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnIdentityUserThatWasNotRequestedMakesTheDepartmentLookupThrowKeyNotFound()
    {
        Members(Member(Ids.Agent1, Ids.DepartmentA1));
        _h.Quietly(() => _h.Identity.GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<StaffUserSummary>>(
                [TicketFixtures.Staff(Ids.Agent1, "One"), TicketFixtures.Staff(Ids.Agent2, "Extra")]))));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Handler().Handle(new ListTicketAgentsQuery(null), Stubs.Ct));
    }
}
