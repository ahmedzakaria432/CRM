using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Queries.GetTicketQueueCounts;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class GetTicketQueueCountsQueryHandlerTests
{
    private static GetTicketQueueCountsQueryHandler Handler(Harness h) => new(h.Tickets, h.User);

    private static Ticket[] Tickets()
    {
        Ticket resolved = TicketFixtures.InStatus(TicketStatus.Open, 6).Assigned(Ids.Actor);
        resolved.Resolve(Ids.Actor, Clock.Now);
        return
        [
            TicketFixtures.InStatus(TicketStatus.New, 1),
            TicketFixtures.InStatus(TicketStatus.Open, 2).Assigned(Ids.Actor),
            TicketFixtures.InStatus(TicketStatus.OnHold, 3),
            TicketFixtures.InStatus(TicketStatus.Open, 4).Assigned(Ids.Agent2),
            TicketFixtures.InStatus(TicketStatus.Closed, 5),
            resolved,
        ];
    }

    [Fact]
    public async Task ThreeCountsAreAskedForMineUnassignedAndAll()
    {
        var h = new Harness();
        List<Specification<Ticket>> specs = [];
        h.Quietly(() => h.Tickets.CountAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                specs.Add(call.Arg<Specification<Ticket>>());
                return Task.FromResult(specs.Count);
            }));

        Result<TicketQueueCountsDto> result = await Handler(h).Handle(new GetTicketQueueCountsQuery(), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(3, specs.Count);
        Assert.All(specs, s => Assert.IsType<TicketSearchSpecification>(s));
        Assert.All(specs, s => Assert.Null(s.Take));
        Assert.Equal(new TicketQueueCountsDto(1, 2, 3), result.Value);
    }

    [Fact]
    public async Task EachCountUsesTheQueueItsNameSays()
    {
        Ticket[] tickets = Tickets();
        var h = new Harness();
        List<Specification<Ticket>> specs = [];
        h.Quietly(() => h.Tickets.CountAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Specification<Ticket> spec = call.Arg<Specification<Ticket>>();
                specs.Add(spec);
                return Task.FromResult(tickets.Count(spec.Criteria?.Compile() ?? (_ => true)));
            }));

        Result<TicketQueueCountsDto> result = await Handler(h).Handle(new GetTicketQueueCountsQuery(), Stubs.Ct);

        // Mine: working tickets assigned to the caller (the Resolved one is not working). Unassigned: working and unassigned.
        // All: every ticket whatever its status.
        Assert.Equal(1, result.Value.Mine);
        Assert.Equal(2, result.Value.Unassigned);
        Assert.Equal(6, result.Value.All);
    }

    [Fact]
    public async Task AnUnparsableUserIdUsesTheEmptyGuid()
    {
        Ticket[] tickets = Tickets();
        var h = new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), "not-a-guid"));
        h.Quietly(() => h.Tickets.Counts(tickets));

        Result<TicketQueueCountsDto> result = await Handler(h).Handle(new GetTicketQueueCountsQuery(), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(0, result.Value.Mine);
        Assert.Equal(2, result.Value.Unassigned);
    }
}
