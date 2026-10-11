using Crm.BuildingBlocks.Application.Paging;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Queries.SearchTickets;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class SearchTicketsQueryHandlerTests
{
    private sealed class Arranged
    {
        public Arranged(IReadOnlyList<TicketListRow> rows, int total, Harness? harness = null)
        {
            H = harness ?? new Harness();
            ReadModel = Substitute.For<ITicketReadModel>();
            ReadModel.ListAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                PageSpecs.Add(call.Arg<Specification<Ticket>>());
                return Task.FromResult(rows);
            });
            H.Quietly(() => H.Tickets.CountAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                CountSpecs.Add(call.Arg<Specification<Ticket>>());
                return Task.FromResult(total);
            }));
        }

        public Harness H { get; }

        public ITicketReadModel ReadModel { get; }

        public List<Specification<Ticket>> PageSpecs { get; } = [];

        public List<Specification<Ticket>> CountSpecs { get; } = [];

        public SearchTicketsQueryHandler Handler() => new(H.Tickets, ReadModel, H.User, H.Assembler());
    }

    // Four tickets with distinct audit stamps, statuses, priorities, assignees, customers and departments.
    private static Ticket[] Sample()
    {
        Ticket a = TicketFixtures.Create(1, subject: "Printer broken").Stamp(Clock.Later(1), null);
        Ticket b = TicketFixtures.Create(2, subject: "Invoice question", customer: Ids.Customer2, priority: TicketPriority.High).Stamp(Clock.Later(2), Clock.Later(20));
        b.Open(Ids.Actor, Clock.Now);
        b.AssignTo(Ids.Actor, Ids.Actor, Clock.Now);
        Ticket c = TicketFixtures.Create(
            3, subject: "login problem", priority: TicketPriority.Urgent, department: Ids.DepartmentA2, branch: Ids.BranchA2).Stamp(Clock.Later(3), Clock.Later(10));
        c.Open(Ids.Actor, Clock.Now);
        c.PutOnHold(Ids.Actor, Clock.Now);
        c.AssignTo(Ids.Agent2, Ids.Actor, Clock.Now);
        Ticket d = TicketFixtures.Create(4, subject: "Old one", priority: TicketPriority.Low).Stamp(Clock.Later(4), Clock.Later(30));
        d.Resolve(Ids.Actor, Clock.Now);
        d.Close(Ids.Actor, Clock.Now);
        return [a, b, c, d];
    }

    private static SearchTicketsQuery Query(
        string? queue = null, string? search = null, IReadOnlyList<string>? statuses = null, IReadOnlyList<string>? priorities = null,
        Guid? category = null, Guid? assignee = null, Guid? department = null, Guid? branch = null, Guid? customer = null,
        string? sort = null, bool descending = true, int page = 1, int pageSize = 20) =>
        new(queue, search, statuses, priorities, category, assignee, department, branch, customer, sort, descending, page, pageSize);

    private static string[] Numbers(IEnumerable<Ticket> tickets) => [.. tickets.Select(t => t.Number.Value)];

    private static string[] Matched(Specification<Ticket> spec, Ticket[] tickets) => Numbers(Stubs.Matching(spec, tickets));

    [Theory]
    [InlineData("mine", new[] { "T-000002" })]
    [InlineData("MINE", new[] { "T-000002" })]
    [InlineData("unassigned", new[] { "T-000001" })]
    [InlineData("UnAssigned", new[] { "T-000001" })]
    [InlineData("all", new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData("ALL", new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData(null, new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData("inbox", new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    public async Task TheQueueIsParsedAndAnUnknownOneFallsBackToAll(string? queue, string[] expected)
    {
        Ticket[] sample = Sample();
        var arranged = new Arranged([], 0);

        Result<PagedResult<TicketListItemDto>> result = await arranged.Handler().Handle(Query(queue: queue), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(expected, Matched(Assert.Single(arranged.PageSpecs), sample));
    }

    [Theory]
    [InlineData("createdAt", true, new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData("CREATEDAT", false, new[] { "T-000001", "T-000002", "T-000003", "T-000004" })]
    [InlineData("price", true, new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData(null, true, new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData("number", false, new[] { "T-000001", "T-000002", "T-000003", "T-000004" })]
    [InlineData("number", true, new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData("priority", true, new[] { "T-000003", "T-000002", "T-000001", "T-000004" })]
    [InlineData("Priority", false, new[] { "T-000004", "T-000001", "T-000002", "T-000003" })]
    [InlineData("modifiedAt", true, new[] { "T-000004", "T-000002", "T-000003", "T-000001" })]
    [InlineData("modifiedAt", false, new[] { "T-000001", "T-000003", "T-000002", "T-000004" })]
    public async Task TheSortKeyIsParsedWithAFallbackToCreatedAt(string? sort, bool descending, string[] expected)
    {
        Ticket[] sample = Sample();
        var arranged = new Arranged([], 0);

        await arranged.Handler().Handle(Query(sort: sort, descending: descending), Stubs.Ct);

        Assert.Equal(expected, Matched(Assert.Single(arranged.PageSpecs), sample));
    }

    [Fact]
    public async Task StatusesAreParsedCaseInsensitivelyAndDistinct()
    {
        Ticket[] sample = Sample();
        var arranged = new Arranged([], 0);

        await arranged.Handler().Handle(Query(statuses: ["open", "Open", "ONHOLD"]), Stubs.Ct);

        Assert.Equal(["T-000003", "T-000002"], Matched(Assert.Single(arranged.PageSpecs), sample));
    }

    [Fact]
    public async Task NullStatusesAndPrioritiesMeanNoFilter()
    {
        Ticket[] sample = Sample();
        var arranged = new Arranged([], 0);

        await arranged.Handler().Handle(Query(statuses: null, priorities: null), Stubs.Ct);

        Assert.Equal(4, Matched(Assert.Single(arranged.PageSpecs), sample).Length);
    }

    [Fact]
    public async Task PrioritiesAreParsedCaseInsensitively()
    {
        Ticket[] sample = Sample();
        var arranged = new Arranged([], 0);

        await arranged.Handler().Handle(Query(priorities: ["high", "URGENT", "High"]), Stubs.Ct);

        Assert.Equal(["T-000003", "T-000002"], Matched(Assert.Single(arranged.PageSpecs), sample));
    }

    [Fact]
    public async Task AnUnknownStatusThatBypassedTheValidatorThrowsArgumentException()
    {
        var arranged = new Arranged([], 0);

        await Assert.ThrowsAsync<ArgumentException>(() => arranged.Handler().Handle(Query(statuses: ["Archived"]), Stubs.Ct));
    }

    [Fact]
    public async Task ANumericStatusParsesBecauseEnumParseAcceptsNumbers()
    {
        Ticket[] sample = Sample();
        var arranged = new Arranged([], 0);

        // 1 is Open.
        await arranged.Handler().Handle(Query(statuses: ["1"]), Stubs.Ct);

        Assert.Equal(["T-000002"], Matched(Assert.Single(arranged.PageSpecs), sample));
    }

    [Fact]
    public async Task AnUnknownPriorityThatBypassedTheValidatorThrowsArgumentException()
    {
        var arranged = new Arranged([], 0);

        await Assert.ThrowsAsync<ArgumentException>(() => arranged.Handler().Handle(Query(priorities: ["Critical"]), Stubs.Ct));
    }

    [Fact]
    public async Task ThePageSpecificationCarriesThePagingAndTheCountSpecificationNone()
    {
        var arranged = new Arranged([], 0);

        await arranged.Handler().Handle(Query(page: 3, pageSize: 10), Stubs.Ct);

        Specification<Ticket> page = Assert.Single(arranged.PageSpecs);
        Specification<Ticket> count = Assert.Single(arranged.CountSpecs);
        Assert.Equal(20, page.Skip);
        Assert.Equal(10, page.Take);
        Assert.Null(count.Skip);
        Assert.Null(count.Take);
    }

    [Fact]
    public async Task BothSpecificationsShareEveryCriterion()
    {
        Ticket[] sample = Sample();
        var arranged = new Arranged([], 0);

        await arranged.Handler().Handle(
            Query(queue: "all", search: "problem", statuses: ["OnHold"], priorities: ["Urgent"], department: Ids.DepartmentA2.Value,
                branch: Ids.BranchA2.Value, customer: Ids.Customer1, assignee: Ids.Agent2),
            Stubs.Ct);

        Func<Ticket, bool> page = Assert.Single(arranged.PageSpecs).Criteria?.Compile() ?? (_ => true);
        Func<Ticket, bool> count = Assert.Single(arranged.CountSpecs).Criteria?.Compile() ?? (_ => true);
        Assert.Equal(sample.Select(page), sample.Select(count));
        Assert.Equal(["T-000003"], Numbers(sample.Where(page)));
    }

    [Fact]
    public async Task EveryFilterIsForwardedToTheSpecification()
    {
        Category category = TicketFixtures.NewCategory(department: Ids.DepartmentA2);
        Ticket[] sample = Sample();
        sample[2].ChangeCategory(category, Ids.Actor, Clock.Now);
        var arranged = new Arranged([], 0);

        await arranged.Handler().Handle(Query(category: category.Id.Value), Stubs.Ct);
        Assert.Equal(["T-000003"], Matched(arranged.PageSpecs[^1], sample));

        await arranged.Handler().Handle(Query(customer: Ids.Customer2), Stubs.Ct);
        Assert.Equal(["T-000002"], Matched(arranged.PageSpecs[^1], sample));

        await arranged.Handler().Handle(Query(assignee: Ids.Agent2), Stubs.Ct);
        Assert.Equal(["T-000003"], Matched(arranged.PageSpecs[^1], sample));

        await arranged.Handler().Handle(Query(department: Ids.DepartmentA1.Value), Stubs.Ct);
        Assert.Equal(["T-000004", "T-000002", "T-000001"], Matched(arranged.PageSpecs[^1], sample));

        await arranged.Handler().Handle(Query(branch: Ids.BranchA2.Value), Stubs.Ct);
        Assert.Equal(["T-000003"], Matched(arranged.PageSpecs[^1], sample));

        await arranged.Handler().Handle(Query(search: "Invoice"), Stubs.Ct);
        Assert.Equal(["T-000002"], Matched(arranged.PageSpecs[^1], sample));
    }

    [Fact]
    public async Task PageAndTotalAreEchoed()
    {
        var arranged = new Arranged([], 57);

        Result<PagedResult<TicketListItemDto>> result = await arranged.Handler().Handle(Query(page: 2, pageSize: 25), Stubs.Ct);

        Assert.Equal(57, result.Value.Total);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(25, result.Value.PageSize);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task AnEmptyPageMakesNoNestedLookups()
    {
        var arranged = new Arranged([], 0);

        await arranged.Handler().Handle(Query(), Stubs.Ct);

        Assert.DoesNotContain("lookup", arranged.H.Log);
    }

    [Fact]
    public async Task RowsAreMappedWithCategoryAssigneeAndCustomer()
    {
        Category category = TicketFixtures.NewCategory(ar: "تقنية", en: "Technical", active: false);
        TicketListRow row = Rows.List(
            Ids.Ticket1, "T-000009", "Printer", TicketStatus.Open, TicketPriority.High, TicketChannel.Portal, category.Id.Value, Ids.Customer1,
            Ids.Contact1, Ids.Agent1, Clock.Now, Clock.Later(1));
        TicketListRow plain = Rows.List(Ids.Ticket2, "T-000010", "Other", customer: Ids.Customer2);
        var arranged = new Arranged([row, plain], 2);
        arranged.H.Quietly(() => arranged.H.Categories.Lists(category));
        arranged.H.KnowsStaff(TicketFixtures.Staff(Ids.Agent1, "Agent One"));
        arranged.H.Quietly(() => arranged.H.Customers.GetCustomerNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<CustomerNameSummary>>(
                [new CustomerNameSummary(Ids.Customer1, "C-000001", "أحمد", "Ahmed")]))));

        Result<PagedResult<TicketListItemDto>> result = await arranged.Handler().Handle(Query(), Stubs.Ct);

        TicketListItemDto first = result.Value.Items[0];
        Assert.Equal(Ids.Ticket1, first.Id);
        Assert.Equal("T-000009", first.Number);
        Assert.Equal("Printer", first.Subject);
        Assert.Equal("Open", first.Status);
        Assert.Equal("High", first.Priority);
        Assert.Equal("Portal", first.Channel);
        Assert.Equal(Ids.Customer1, first.CustomerId);
        Assert.Equal(Ids.Contact1, first.ContactId);
        Assert.Equal(category.Id.Value, first.Category?.Id);
        Assert.False(first.Category?.IsActive);
        Assert.Equal("Technical", first.Category?.Name.En);
        Assert.Equal(Ids.Agent1, first.AssignedAgentId);
        Assert.Equal("Agent One", first.AssignedAgentName);
        Assert.Equal(Ids.BranchA1.Value, first.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, first.DepartmentId);
        Assert.Equal(Clock.Now, first.CreatedAtUtc);
        Assert.Equal(Clock.Later(1), first.ModifiedAtUtc);
        Assert.Equal(new TicketCustomerRefDto(Ids.Customer1, "C-000001", "أحمد", "Ahmed"), first.Customer);
        TicketListItemDto second = result.Value.Items[1];
        Assert.Null(second.Category);
        Assert.Null(second.AssignedAgentName);
        Assert.Null(second.Customer);
    }

    [Fact]
    public async Task ACustomerLookupFailureLeavesTheCustomerNullAndThePageSucceeds()
    {
        var arranged = new Arranged([Rows.List()], 1);
        arranged.H.Quietly(() => arranged.H.Customers.GetCustomerNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IReadOnlyList<CustomerNameSummary>>(Error.Forbidden("Organisation.OutOfScope", "x")))));

        Result<PagedResult<TicketListItemDto>> result = await arranged.Handler().Handle(Query(), Stubs.Ct);

        Expect.Success(result);
        Assert.Null(Assert.Single(result.Value.Items).Customer);
    }

    [Fact]
    public async Task AnIdentityFailureLeavesTheAssigneeNameNull()
    {
        var arranged = new Arranged([Rows.List(agent: Ids.Agent1)], 1);
        arranged.H.Quietly(() => arranged.H.Identity.GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IReadOnlyList<StaffUserSummary>>(Error.Forbidden("Organisation.OutOfScope", "x")))));

        Result<PagedResult<TicketListItemDto>> result = await arranged.Handler().Handle(Query(), Stubs.Ct);

        Expect.Success(result);
        TicketListItemDto item = Assert.Single(result.Value.Items);
        Assert.Equal(Ids.Agent1, item.AssignedAgentId);
        Assert.Null(item.AssignedAgentName);
    }

    [Fact]
    public async Task TheCallersIdDrivesTheMineQueue()
    {
        Ticket[] sample = Sample();
        var arranged = new Arranged([], 0, new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), Ids.Agent2.ToString())));

        await arranged.Handler().Handle(Query(queue: "mine"), Stubs.Ct);

        Assert.Equal(["T-000003"], Matched(Assert.Single(arranged.PageSpecs), sample));
    }
}
