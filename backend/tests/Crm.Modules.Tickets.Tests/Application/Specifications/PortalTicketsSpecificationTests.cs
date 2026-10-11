using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Specifications;

public sealed class PortalTicketsSpecificationTests
{
    private static readonly IReadOnlyList<TicketStatus> NoStatuses = [];

    // Tickets of customer 1 (contacts 1 and 2), customer 2 (contact 1 happens to be the same contact id), with stamps.
    private static Ticket[] Tickets()
    {
        Ticket a = TicketFixtures.Create(1, subject: "Printer broken", contact: Ids.Contact1).Stamp(Clock.Later(1), null);
        Ticket b = TicketFixtures.Create(2, subject: "Invoice question", contact: Ids.Contact2).Stamp(Clock.Later(2), Clock.Later(10));
        b.Open(Ids.Actor, Clock.Now);
        Ticket c = TicketFixtures.Create(3, subject: "Login", contact: Ids.Contact1, customer: Ids.Customer2).Stamp(Clock.Later(3), null);
        Ticket d = TicketFixtures.Create(4, subject: "No contact at all", contact: null).Stamp(Clock.Later(4), Clock.Later(10));
        return [a, b, c, d];
    }

    private static string[] Numbers(IEnumerable<Ticket> tickets) => [.. tickets.Select(t => t.Number.Value)];

    private static PortalTicketsSpecification Spec(
        PortalCustomerGrant grant, TicketId? id = null, IReadOnlyList<TicketStatus>? statuses = null, string? search = null,
        int? page = null, int? pageSize = null, bool tracked = false) =>
        new(grant, id, statuses ?? NoStatuses, search, page, pageSize, tracked);

    [Fact]
    public void ANonAdminSeesOnlyTheirOwnContactsTicketsOfTheirCustomer()
    {
        Assert.Equal(["T-000001"], Numbers(Stubs.Matching(Spec(Scopes.Grant(Ids.Customer1, Ids.Contact1)), Tickets())));
    }

    [Fact]
    public void AnAccountAdminSeesEveryTicketOfTheCustomerIncludingOnesWithoutAContact()
    {
        Assert.Equal(
            ["T-000002", "T-000004", "T-000001"],
            Numbers(Stubs.Matching(Spec(Scopes.Grant(Ids.Customer1, Ids.Contact1, admin: true)), Tickets())));
    }

    [Fact]
    public void ACustomersTicketsAreNeverVisibleToAnotherCustomer()
    {
        Assert.Equal(["T-000003"], Numbers(Stubs.Matching(Spec(Scopes.Grant(Ids.Customer2, Ids.Contact1, admin: true)), Tickets())));
    }

    [Fact]
    public void AnIdFilterKeepsOnlyThatTicket()
    {
        Ticket[] tickets = Tickets();

        Assert.Equal(["T-000002"], Numbers(Stubs.Matching(Spec(Scopes.Grant(admin: true), tickets[1].Id), tickets)));
    }

    [Fact]
    public void AnIdOfAnotherCustomersTicketMatchesNothing()
    {
        Ticket[] tickets = Tickets();

        Assert.Empty(Stubs.Matching(Spec(Scopes.Grant(Ids.Customer1, admin: true), tickets[2].Id), tickets));
    }

    [Fact]
    public void EmptyStatusesMeanAnyStatusAndAListNarrows()
    {
        Ticket[] tickets = Tickets();
        PortalCustomerGrant admin = Scopes.Grant(admin: true);

        Assert.Equal(3, Stubs.Matching(Spec(admin), tickets).Count);
        Assert.Equal(["T-000002"], Numbers(Stubs.Matching(Spec(admin, statuses: [TicketStatus.Open]), tickets)));
        Assert.Equal(["T-000004", "T-000001"], Numbers(Stubs.Matching(Spec(admin, statuses: [TicketStatus.New]), tickets)));
    }

    [Theory]
    [InlineData("Invoice", new[] { "T-000002" })]
    [InlineData("  Invoice  ", new[] { "T-000002" })]
    [InlineData("T-000004", new[] { "T-000004" })]
    [InlineData("T-0000", new[] { "T-000002", "T-000004", "T-000001" })]
    [InlineData("", new[] { "T-000002", "T-000004", "T-000001" })]
    [InlineData("   ", new[] { "T-000002", "T-000004", "T-000001" })]
    [InlineData(null, new[] { "T-000002", "T-000004", "T-000001" })]
    [InlineData("nomatch", new string[0])]
    public void TheSearchMatchesNumberOrSubjectAndABlankOneMatchesAll(string? search, string[] expected)
    {
        Assert.Equal(expected, Numbers(Stubs.Matching(Spec(Scopes.Grant(admin: true), search: search), Tickets())));
    }

    [Fact]
    public void TicketsAreOrderedByModifiedOrCreatedDescendingThenNumber()
    {
        // d and b share the same modified time (+10): the number breaks the tie ascending; a (created +1) comes last.
        Assert.Equal(["T-000002", "T-000004", "T-000001"], Numbers(Stubs.Matching(Spec(Scopes.Grant(admin: true)), Tickets())));
    }

    [Fact]
    public void AMissingTimestampSortsLast()
    {
        Ticket stamped = TicketFixtures.Create(1).Stamp(Clock.Later(1), null);
        Ticket unstamped = TicketFixtures.Create(2);

        Assert.Equal(["T-000001", "T-000002"], Numbers(Stubs.Matching(Spec(Scopes.Grant(admin: true)), stamped, unstamped)));
    }

    [Theory]
    [InlineData(1, 2, 0, 2)]
    [InlineData(3, 10, 20, 10)]
    [InlineData(2, 5, 5, 5)]
    public void PagingIsAppliedOnlyWhenBothArgumentsAreGiven(int page, int size, int skip, int take)
    {
        PortalTicketsSpecification spec = Spec(Scopes.Grant(), page: page, pageSize: size);

        Assert.Equal(skip, spec.Skip);
        Assert.Equal(take, spec.Take);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(1, null)]
    [InlineData(null, 10)]
    public void NoPagingWithoutBothArguments(int? page, int? size)
    {
        PortalTicketsSpecification spec = Spec(Scopes.Grant(), page: page, pageSize: size);

        Assert.Null(spec.Skip);
        Assert.Null(spec.Take);
    }

    [Fact]
    public void TrackedMeansNotReadOnlyAndUntrackedMeansReadOnly()
    {
        Assert.False(Spec(Scopes.Grant(), tracked: true).AsNoTracking);
        Assert.True(Spec(Scopes.Grant(), tracked: false).AsNoTracking);
    }

    [Fact]
    public void ANullGrantOrStatusListIsRefused()
    {
        Func<PortalCustomerGrant, TicketId?, IReadOnlyList<TicketStatus>, string?, int?, int?, bool, PortalTicketsSpecification> create =
            (g, i, s, q, p, z, t) => new PortalTicketsSpecification(g, i, s, q, p, z, t);

        NullArguments.AssertThrowsArgumentNull(create, null, null, NoStatuses, null, null, null, false);
        NullArguments.AssertThrowsArgumentNull(create, Scopes.Grant(), null, null, null, null, null, false);
    }

    [Fact]
    public void TheSpecificationDoesNotLiftAnyScopeFilter()
    {
        PortalTicketsSpecification spec = Spec(Scopes.Grant());

        Assert.False(spec.IgnoreOrganisationScopeFilter);
        Assert.False(spec.IgnoreSoftDeleteFilter);
    }
}
