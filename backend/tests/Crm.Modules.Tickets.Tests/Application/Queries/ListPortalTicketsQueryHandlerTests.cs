using Crm.BuildingBlocks.Application.Paging;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Queries.ListPortalTickets;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class ListPortalTicketsQueryHandlerTests
{
    private readonly IPortalTicketReadModel _readModel = Substitute.For<IPortalTicketReadModel>();
    private readonly List<Specification<Ticket>> _pageSpecs = [];
    private readonly List<Specification<Ticket>> _countSpecs = [];
    private readonly Harness _h = new(user: CurrentUsers.Portal(Scopes.Grant()));

    public ListPortalTicketsQueryHandlerTests()
    {
        _readModel.ListAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _pageSpecs.Add(call.Arg<Specification<Ticket>>());
            return Task.FromResult<IReadOnlyList<PortalTicketRow>>([]);
        });
        _h.Quietly(() => _h.Tickets.CountAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _countSpecs.Add(call.Arg<Specification<Ticket>>());
            return Task.FromResult(42);
        }));
    }

    private ListPortalTicketsQueryHandler Handler(Harness? harness = null) => new(
        (harness ?? _h).Tickets, _readModel, (harness ?? _h).User, (harness ?? _h).Mapper());

    private static Ticket[] OneOfEach()
    {
        Ticket[] tickets = [.. Enum.GetValues<TicketStatus>().Select((s, i) => TicketFixtures.InStatus(s, i + 1, Ids.Contact1))];
        return tickets;
    }

    private static Func<Ticket, bool> Predicate(Specification<Ticket> spec) => spec.Criteria?.Compile() ?? (_ => true);

    private static string[] StatusesKept(Specification<Ticket> spec, Ticket[] tickets) =>
        [.. tickets.Where(spec.Criteria?.Compile() ?? (_ => true)).Select(t => t.Status.ToString()).Order(StringComparer.Ordinal)];

    [Fact]
    public async Task ANonPortalCallerIsUnauthorized()
    {
        var h = new Harness();

        Result<PagedResult<PortalTicketSummaryDto>> result = await Handler(h).Handle(new ListPortalTicketsQuery(null, null, null), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.PortalOnly, ErrorType.Unauthorized);
        Assert.Empty(_pageSpecs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("open")]
    [InlineData("OPEN")]
    [InlineData("unknown")]
    public async Task ANullOrUnknownTabListsTheOpenTab(string? tab)
    {
        await Handler().Handle(new ListPortalTicketsQuery(tab, null, null), Stubs.Ct);

        Assert.Equal(["New", "OnHold", "Open", "PendingCustomer"], StatusesKept(Assert.Single(_pageSpecs), OneOfEach()));
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("CLOSED")]
    public async Task TheClosedTabListsResolvedClosedAndCancelled(string tab)
    {
        await Handler().Handle(new ListPortalTicketsQuery(tab, null, null), Stubs.Ct);

        Assert.Equal(["Cancelled", "Closed", "Resolved"], StatusesKept(Assert.Single(_pageSpecs), OneOfEach()));
    }

    [Theory]
    [InlineData("open", "InProgress", new[] { "OnHold", "Open" })]
    [InlineData("open", "inprogress", new[] { "OnHold", "Open" })]
    [InlineData("open", "Received", new[] { "New" })]
    [InlineData("open", "AwaitingYourReply", new[] { "PendingCustomer" })]
    [InlineData("closed", "Closed", new[] { "Cancelled", "Closed" })]
    [InlineData("closed", "Resolved", new[] { "Resolved" })]
    public async Task APortalStatusNarrowsTheTab(string tab, string status, string[] expected)
    {
        await Handler().Handle(new ListPortalTicketsQuery(tab, status, null), Stubs.Ct);

        Assert.Equal(expected, StatusesKept(Assert.Single(_pageSpecs), OneOfEach()));
    }

    [Theory]
    [InlineData("open", "Resolved")]
    [InlineData("open", "Closed")]
    [InlineData("closed", "Received")]
    [InlineData("closed", "InProgress")]
    [InlineData("open", "Nonsense")]
    public async Task AStatusOutsideTheTabIsAnEmptySuccessfulPageAndNothingIsRead(string tab, string status)
    {
        Result<PagedResult<PortalTicketSummaryDto>> result = await Handler().Handle(new ListPortalTicketsQuery(tab, status, null, 2, 10), Stubs.Ct);

        Expect.Success(result);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.Total);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(10, result.Value.PageSize);
        Assert.Empty(_pageSpecs);
        Assert.Empty(_countSpecs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ABlankStatusMeansTheWholeTab(string? status)
    {
        await Handler().Handle(new ListPortalTicketsQuery("open", status, null), Stubs.Ct);

        Assert.Equal(["New", "OnHold", "Open", "PendingCustomer"], StatusesKept(Assert.Single(_pageSpecs), OneOfEach()));
    }

    [Fact]
    public async Task TheSearchIsPassedThrough()
    {
        Ticket[] tickets = [TicketFixtures.InStatus(TicketStatus.Open, 1, Ids.Contact1), TicketFixtures.InStatus(TicketStatus.Open, 2, Ids.Contact1)];

        await Handler().Handle(new ListPortalTicketsQuery("open", null, "T-000002"), Stubs.Ct);

        Assert.Equal(["T-000002"], tickets.Where(Predicate(Assert.Single(_pageSpecs))).Select(t => t.Number.Value));
    }

    [Fact]
    public async Task ThePageSpecificationHasPagingAndTheCountSpecificationHasNone()
    {
        await Handler().Handle(new ListPortalTicketsQuery("open", null, null, 3, 10), Stubs.Ct);

        Assert.Equal(20, Assert.Single(_pageSpecs).Skip);
        Assert.Equal(10, Assert.Single(_pageSpecs).Take);
        Assert.Null(Assert.Single(_countSpecs).Skip);
        Assert.Null(Assert.Single(_countSpecs).Take);
        Assert.All([.. _pageSpecs, .. _countSpecs], s => Assert.True(s.AsNoTracking));
    }

    [Fact]
    public async Task TheTotalComesFromTheCountAndSummariesFromTheMapper()
    {
        var readModel = Substitute.For<IPortalTicketReadModel>();
        readModel.ListAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<PortalTicketRow>>(
            [Rows.Portal(Ids.Ticket1, status: TicketStatus.PendingCustomer), Rows.Portal(Ids.Ticket2, "T-000002", status: TicketStatus.Open)]));
        var handler = new ListPortalTicketsQueryHandler(_h.Tickets, readModel, _h.User, _h.Mapper());

        Result<PagedResult<PortalTicketSummaryDto>> result = await handler.Handle(new ListPortalTicketsQuery(null, null, null, 4, 15), Stubs.Ct);

        Assert.Equal(42, result.Value.Total);
        Assert.Equal(4, result.Value.Page);
        Assert.Equal(15, result.Value.PageSize);
        Assert.Equal(["AwaitingYourReply", "InProgress"], result.Value.Items.Select(i => i.Status));
    }

    [Fact]
    public void TheDefaultPageIsOneOfTwenty()
    {
        var query = new ListPortalTicketsQuery(null, null, null);

        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    [Fact]
    public async Task AnAccountAdminListsEveryTicketOfTheCustomerAndAContactOnlyItsOwn()
    {
        Ticket mine = TicketFixtures.InStatus(TicketStatus.Open, 1, Ids.Contact1);
        Ticket colleagues = TicketFixtures.InStatus(TicketStatus.Open, 2, Ids.Contact2);
        Ticket other = TicketFixtures.InStatus(TicketStatus.Open, 3, Ids.Contact2, Ids.Customer2);

        await Handler().Handle(new ListPortalTicketsQuery(null, null, null), Stubs.Ct);
        var admin = new Harness(user: CurrentUsers.Portal(Scopes.Grant(admin: true)));
        await Handler(admin).Handle(new ListPortalTicketsQuery(null, null, null), Stubs.Ct);

        Assert.Equal(["T-000001"], new[] { mine, colleagues, other }.Where(Predicate(_pageSpecs[0])).Select(t => t.Number.Value));
        Assert.Equal(["T-000001", "T-000002"], new[] { mine, colleagues, other }.Where(Predicate(_pageSpecs[1])).Select(t => t.Number.Value));
    }
}
