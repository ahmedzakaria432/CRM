using Crm.BuildingBlocks.Application.Paging;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Queries.ListPortalTickets;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application;

/// <summary><c>PortalTicketStatuses</c> is internal; it is reached through the mapper (status names) and ListPortalTickets (tabs).</summary>
public sealed class PortalTicketStatusesTests
{
    public static TheoryData<TicketStatus, string> Mapping() => new()
    {
        { TicketStatus.New, "Received" },
        { TicketStatus.Open, "InProgress" },
        { TicketStatus.OnHold, "InProgress" },
        { TicketStatus.PendingCustomer, "AwaitingYourReply" },
        { TicketStatus.Resolved, "Resolved" },
        { TicketStatus.Closed, "Closed" },
        { TicketStatus.Cancelled, "Closed" },
    };

    [Theory]
    [MemberData(nameof(Mapping))]
    public async Task EachDomainStatusMapsToItsPortalName(TicketStatus status, string expected)
    {
        var h = new Harness();

        PortalTicketDto fromRow = await h.Mapper().ToDtoAsync(Rows.Portal(status: status), Stubs.Ct);
        PortalTicketDto fromTicket = await h.Mapper().ToDtoAsync(TicketFixtures.InStatus(status), Stubs.Ct);

        Assert.Equal(expected, fromRow.Status);
        Assert.Equal(expected, fromTicket.Status);
    }

    [Fact]
    public void EveryDomainStatusIsCovered()
    {
        Assert.Equal(Enum.GetValues<TicketStatus>().Length, Mapping().Count);
    }

    [Theory]
    [InlineData("open", "New,Open,PendingCustomer,OnHold")]
    [InlineData("closed", "Resolved,Closed,Cancelled")]
    [InlineData("Closed", "Resolved,Closed,Cancelled")]
    [InlineData(null, "New,Open,PendingCustomer,OnHold")]
    public async Task TabsHoldTheDocumentedStatuses(string? tab, string expected)
    {
        var readModel = Substitute.For<IPortalTicketReadModel>();
        List<Specification<Ticket>> specs = [];
        readModel.ListAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            specs.Add(call.Arg<Specification<Ticket>>());
            return Task.FromResult<IReadOnlyList<PortalTicketRow>>([]);
        });
        var h = new Harness(user: CurrentUsers.Portal(Scopes.Grant()));
        var handler = new ListPortalTicketsQueryHandler(h.Tickets, readModel, h.User, h.Mapper());

        Result<PagedResult<PortalTicketSummaryDto>> result = await handler.Handle(new ListPortalTicketsQuery(tab, null, null), Stubs.Ct);

        Expect.Success(result);
        Func<Ticket, bool> keeps = Assert.Single(specs).Criteria?.Compile() ?? (_ => true);
        string[] kept = [.. Enum.GetValues<TicketStatus>().Where(s => keeps(TicketFixtures.InStatus(s, contact: Ids.Contact1))).Select(s => s.ToString())];
        Assert.Equal(expected.Split(',').Order(StringComparer.Ordinal), kept.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("received", "New")]
    [InlineData("RECEIVED", "New")]
    [InlineData("InProgress", "Open,OnHold")]
    [InlineData("awaitingyourreply", "PendingCustomer")]
    public async Task APortalStatusFilterIsCaseInsensitive(string name, string expected)
    {
        var readModel = Substitute.For<IPortalTicketReadModel>();
        List<Specification<Ticket>> specs = [];
        readModel.ListAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            specs.Add(call.Arg<Specification<Ticket>>());
            return Task.FromResult<IReadOnlyList<PortalTicketRow>>([]);
        });
        var h = new Harness(user: CurrentUsers.Portal(Scopes.Grant()));
        var handler = new ListPortalTicketsQueryHandler(h.Tickets, readModel, h.User, h.Mapper());

        await handler.Handle(new ListPortalTicketsQuery("open", name, null), Stubs.Ct);

        Func<Ticket, bool> keeps = Assert.Single(specs).Criteria?.Compile() ?? (_ => true);
        string[] kept = [.. Enum.GetValues<TicketStatus>().Where(s => keeps(TicketFixtures.InStatus(s, contact: Ids.Contact1))).Select(s => s.ToString())];
        Assert.Equal(expected.Split(',').Order(StringComparer.Ordinal), kept.Order(StringComparer.Ordinal));
    }
}
