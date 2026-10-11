using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Queries.GetPortalTicketThread;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class GetPortalTicketThreadQueryHandlerTests
{
    private readonly IPortalTicketReadModel _portalTickets = Substitute.For<IPortalTicketReadModel>();
    private readonly ITicketThreadReadModel _threads = Substitute.For<ITicketThreadReadModel>();
    private readonly TicketId _id = new(Ids.Ticket1);

    private GetPortalTicketThreadQueryHandler Handler(Harness h) => new(_portalTickets, _threads, h.User);

    private static Harness Portal() => new(user: CurrentUsers.Portal(Scopes.Grant()));

    private void Owns(bool owned) =>
        _portalTickets.ListAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PortalTicketRow>>(owned ? [Rows.Portal()] : []));

    private void PublicRows(IReadOnlyList<TicketThreadRow>? rows) =>
        _threads.ListPublicAsync(Arg.Any<TicketId>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(rows));

    [Fact]
    public async Task ANonPortalCallerIsUnauthorized()
    {
        Result<PortalThreadPageDto> result = await Handler(new Harness()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, null), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.PortalOnly, ErrorType.Unauthorized);
    }

    [Fact]
    public async Task OwnershipIsCheckedFirstAndAMissingTicketNeverReachesTheThread()
    {
        Owns(false);

        Result<PortalThreadPageDto> result = await Handler(Portal()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, null), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        await _threads.DidNotReceive().ListPublicAsync(Arg.Any<TicketId>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnlyListPublicAsyncIsUsedNeverListAsync()
    {
        Owns(true);
        PublicRows([]);

        await Handler(Portal()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, null), Stubs.Ct);

        await _threads.Received(1).ListPublicAsync(_id, 0, 51, Arg.Any<CancellationToken>());
        await _threads.DidNotReceive().ListAsync(
            Arg.Any<TicketId>(), Arg.Any<IReadOnlyCollection<TicketThreadVisibility>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OwnershipIsAskedWithAReadOnlyPortalSpecification()
    {
        Owns(true);
        PublicRows([]);

        await Handler(Portal()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, null), Stubs.Ct);

        await _portalTickets.Received(1).ListAsync(
            Arg.Is<Specification<Ticket>>(s => s is PortalTicketsSpecification && s.AsNoTracking), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANullPublicThreadIsNotFound()
    {
        Owns(true);
        PublicRows(null);

        Result<PortalThreadPageDto> result = await Handler(Portal()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, null), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
    }

    [Fact]
    public async Task TheSenderIsCustomerForInboundAndSupportForOutbound()
    {
        Owns(true);
        PublicRows(
        [
            Rows.Message(1, TicketMessageDirection.Inbound, Ids.Contact1),
            Rows.Message(2, TicketMessageDirection.Outbound, Ids.Agent1, Ids.Agent1),
        ]);

        Result<PortalThreadPageDto> result = await Handler(Portal()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, null), Stubs.Ct);

        Assert.Equal(["Customer", "Support"], result.Value.Items.Select(i => i.Sender));
    }

    [Fact]
    public async Task IsYouIsTrueOnlyForAnInboundMessageOfTheCallersContact()
    {
        Owns(true);
        PublicRows(
        [
            Rows.Message(1, TicketMessageDirection.Inbound, Ids.Contact1),
            Rows.Message(2, TicketMessageDirection.Inbound, Ids.Contact2),
            Rows.Message(3, TicketMessageDirection.Inbound, null),
            Rows.Message(4, TicketMessageDirection.Outbound, Ids.Contact1),
        ]);

        Result<PortalThreadPageDto> result = await Handler(Portal()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, null), Stubs.Ct);

        Assert.Equal([true, false, false, false], result.Value.Items.Select(i => i.IsYou));
    }

    [Fact]
    public async Task ANullBodyBecomesAnEmptyString()
    {
        Owns(true);
        TicketThreadRow row = Rows.Message(1) with { Body = null };
        PublicRows([row]);

        Result<PortalThreadPageDto> result = await Handler(Portal()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, null), Stubs.Ct);

        Assert.Equal(string.Empty, Assert.Single(result.Value.Items).Body);
    }

    [Theory]
    [InlineData(null, 50, 0, 51)]
    [InlineData(5, 10, 5, 11)]
    [InlineData(0, 1, 0, 2)]
    public async Task TheCursorAndTheTakeIncludeOneExtraRow(int? after, int limit, int expectedAfter, int expectedTake)
    {
        Owns(true);
        PublicRows([]);

        await Handler(Portal()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, after, limit), Stubs.Ct);

        await _threads.Received(1).ListPublicAsync(_id, expectedAfter, expectedTake, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheNextCursorIsTheLastItemsSequenceOnlyWhenMoreThanTheLimitCame()
    {
        Owns(true);
        PublicRows([Rows.Message(4), Rows.Message(5), Rows.Message(6)]);

        Result<PortalThreadPageDto> more = await Handler(Portal()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, 3, 2), Stubs.Ct);

        Assert.Equal([4, 5], more.Value.Items.Select(i => i.Sequence));
        Assert.Equal(5, more.Value.NextCursor);

        PublicRows([Rows.Message(4), Rows.Message(5)]);
        Result<PortalThreadPageDto> last = await Handler(Portal()).Handle(new GetPortalTicketThreadQuery(Ids.Ticket1, 3, 2), Stubs.Ct);

        Assert.Equal(2, last.Value.Items.Count);
        Assert.Null(last.Value.NextCursor);
    }
}
