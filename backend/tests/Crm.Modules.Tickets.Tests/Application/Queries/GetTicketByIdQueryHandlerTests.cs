using Crm.Modules.Identity.Contracts;
using Crm.Modules.Tickets.Application.Queries.GetTicketById;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class GetTicketByIdQueryHandlerTests
{
    private static GetTicketByIdQueryHandler Handler(Harness h) => new(h.Tickets, h.Assembler());

    [Fact]
    public async Task AnUnknownTicketIsNotFound()
    {
        var h = new Harness();
        h.Quietly(() => h.Tickets.FindsFirstOf());

        Result<TicketDto> result = await Handler(h).Handle(new GetTicketByIdQuery(Ids.Ticket1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Contains(Ids.Ticket1.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTicketIsReadThroughTheByIdSpecificationNotTheRepositoryGetter()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        var h = new Harness();
        h.Quietly(() => h.Tickets.FindsFirstOf(ticket));

        await Handler(h).Handle(new GetTicketByIdQuery(ticket.Id.Value), Stubs.Ct);

        await h.Tickets.Received(1).FirstOrDefaultAsync(Arg.Any<Crm.BuildingBlocks.Application.Specifications.Specification<Ticket>>(), Arg.Any<CancellationToken>());
        await h.Tickets.DidNotReceive().GetByIdAsync(Arg.Any<TicketId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheDtoCarriesEveryField()
    {
        Category category = TicketFixtures.NewCategory(ar: "تقنية", en: "Technical");
        Ticket ticket = TicketFixtures.Create(
            subject: "Printer", description: "Broken", customer: Ids.Customer2, contact: Ids.Contact1, category: category,
            priority: TicketPriority.High, id: new TicketId(Ids.Ticket1));
        ticket.Open(Ids.Agent1, Clock.Later(1));
        ticket.AssignTo(Ids.Agent2, Ids.Agent1, Clock.Later(2));
        ticket.ReplyAsAgent("hi", Ids.Agent2, Clock.Later(3));
        ticket.Resolve(Ids.Agent2, Clock.Later(4));
        ticket.Stamp(Clock.Now, Clock.Later(5), "creator");
        category.Deactivate();

        var h = new Harness();
        h.Quietly(() => h.Tickets.FindsFirstOf(ticket));
        h.Holding(category);
        h.KnowsStaff(TicketFixtures.Staff(Ids.Actor, "Creator"), TicketFixtures.Staff(Ids.Agent1, "Agent One"), TicketFixtures.Staff(Ids.Agent2, "Agent Two"));

        Result<TicketDto> result = await Handler(h).Handle(new GetTicketByIdQuery(Ids.Ticket1), Stubs.Ct);

        Expect.Success(result);
        TicketDto dto = result.Value;
        Assert.Equal(Ids.Ticket1, dto.Id);
        Assert.Equal("T-000001", dto.Number);
        Assert.Equal("Printer", dto.Subject);
        Assert.Equal("Broken", dto.Description);
        Assert.Equal("Resolved", dto.Status);
        Assert.Equal("High", dto.Priority);
        Assert.Equal("Agent", dto.Channel);
        Assert.Equal(Ids.Customer2, dto.CustomerId);
        Assert.Equal(Ids.Contact1, dto.ContactId);
        Assert.Equal(category.Id.Value, dto.Category?.Id);
        Assert.False(dto.Category?.IsActive);
        Assert.Equal("Technical", dto.Category?.Name.En);
        Assert.Equal(Ids.Agent2, dto.AssignedAgentId);
        Assert.Equal("Agent Two", dto.AssignedAgentName);
        Assert.Equal(Ids.TenantA.Value, dto.TenantId);
        Assert.Equal(Ids.BranchA1.Value, dto.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, dto.DepartmentId);
        Assert.Equal(Clock.Now, dto.CreatedAtUtc);
        Assert.Equal("creator", dto.CreatedBy);
        Assert.Equal(Clock.Later(5), dto.ModifiedAtUtc);
        Assert.Equal(Clock.Later(3), dto.FirstRespondedAtUtc);
        Assert.Equal(Clock.Later(4), dto.ResolvedAtUtc);
        Assert.Null(dto.ClosedAtUtc);
        Assert.Equal(["Reopen", "AddNote"], dto.AllowedActions.Where(a => a is "Reopen" or "AddNote").ToArray());
        Assert.Equal(ticket.AllowedActions().Select(a => a.ToString()), dto.AllowedActions);
        Assert.Empty(dto.RowVersion);
    }

    [Fact]
    public async Task ActivitiesAreOrderedBySequenceAndPeopleAreNamed()
    {
        Ticket ticket = TicketFixtures.Create();
        ticket.Open(Ids.Agent1, Clock.Later(1));
        ticket.AssignTo(Ids.Agent2, Ids.Agent1, Clock.Later(2));
        var h = new Harness();
        h.Quietly(() => h.Tickets.FindsFirstOf(ticket));
        h.KnowsStaff(TicketFixtures.Staff(Ids.Agent1, "Agent One"), TicketFixtures.Staff(Ids.Agent2, "Agent Two"), TicketFixtures.Staff(Ids.Actor, "Creator"));

        Result<TicketDto> result = await Handler(h).Handle(new GetTicketByIdQuery(ticket.Id.Value), Stubs.Ct);

        Assert.Equal(
            ["Created", "Opened", "Assigned"],
            result.Value.Activities.Select(a => a.Kind));
        Assert.Equal(
            ["Agent One", "Agent Two", "Creator"],
            result.Value.People.Select(p => p.DisplayName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnIdentityFailureLeavesTheNamesOutButStillSucceeds()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);
        var h = new Harness();
        h.Quietly(() =>
        {
            h.Tickets.FindsFirstOf(ticket);
            h.Identity.GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Failure<IReadOnlyList<StaffUserSummary>>(Error.Forbidden("Organisation.OutOfScope", "x"))));
        });

        Result<TicketDto> result = await Handler(h).Handle(new GetTicketByIdQuery(ticket.Id.Value), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(Ids.Agent1, result.Value.AssignedAgentId);
        Assert.Null(result.Value.AssignedAgentName);
        Assert.Empty(result.Value.People);
    }
}
