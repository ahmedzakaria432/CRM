using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Specifications;

public sealed class TicketSearchSpecificationTests
{
    private static readonly IReadOnlyList<TicketStatus> NoStatuses = [];
    private static readonly IReadOnlyList<TicketPriority> NoPriorities = [];

    // a: New/Normal/unassigned/customer1/dept A1; b: Open/High/Actor/customer2/dept A1; c: OnHold/Urgent/Agent2/dept A2 with category;
    // d: Resolved/Low/unassigned. Created +1..+4, modified only for b and c.
    private static (Ticket[] All, Category Category) Sample()
    {
        Category category = TicketFixtures.NewCategory(department: Ids.DepartmentA2);
        Ticket a = TicketFixtures.Create(1, subject: "Printer broken").Stamp(Clock.Later(1), null);
        Ticket b = TicketFixtures.Create(2, subject: "Invoice question", customer: Ids.Customer2, priority: TicketPriority.High).Stamp(Clock.Later(2), Clock.Later(20));
        b.Open(Ids.Actor, Clock.Now);
        b.AssignTo(Ids.Actor, Ids.Actor, Clock.Now);
        Ticket c = TicketFixtures.Create(
            3, subject: "login problem", priority: TicketPriority.Urgent, department: Ids.DepartmentA2, branch: Ids.BranchA2, category: category)
            .Stamp(Clock.Later(3), Clock.Later(10));
        c.Open(Ids.Actor, Clock.Now);
        c.PutOnHold(Ids.Actor, Clock.Now);
        c.AssignTo(Ids.Agent2, Ids.Actor, Clock.Now);
        Ticket d = TicketFixtures.Create(4, subject: "Old one", priority: TicketPriority.Low).Stamp(Clock.Later(4), Clock.Later(30));
        d.Resolve(Ids.Actor, Clock.Now);
        return ([a, b, c, d], category);
    }

    private static TicketSearchSpecification Spec(
        TicketQueueKind queue = TicketQueueKind.All, Guid? me = null, string? search = null, IReadOnlyList<TicketStatus>? statuses = null,
        IReadOnlyList<TicketPriority>? priorities = null, Guid? category = null, Guid? assignee = null, Guid? department = null,
        Guid? branch = null, Guid? customer = null, TicketSort sort = TicketSort.CreatedAt, bool descending = true, int? page = null,
        int? pageSize = null) =>
        new(
            queue, me ?? Ids.Actor, search, statuses ?? NoStatuses, priorities ?? NoPriorities, category, assignee, department, branch,
            customer, sort, descending, page, pageSize);

    private static string[] Run(TicketSearchSpecification spec, Ticket[] tickets) =>
        [.. Stubs.Matching(spec, tickets).Select(t => t.Number.Value)];

    [Fact]
    public void TheAllQueueWithNoStatusesKeepsEveryStatus()
    {
        Assert.Equal(["T-000004", "T-000003", "T-000002", "T-000001"], Run(Spec(), Sample().All));
    }

    [Theory]
    [InlineData(TicketQueueKind.Mine)]
    [InlineData(TicketQueueKind.Unassigned)]
    public void MineAndUnassignedDefaultToTheFourWorkingStatuses(TicketQueueKind queue)
    {
        Ticket[] all = Sample().All;

        string[] kept = Run(Spec(queue), all);

        Assert.DoesNotContain("T-000004", kept);
    }

    [Fact]
    public void MineKeepsWhatIsAssignedToMe()
    {
        Assert.Equal(["T-000002"], Run(Spec(TicketQueueKind.Mine), Sample().All));
        Assert.Equal(["T-000003"], Run(Spec(TicketQueueKind.Mine, me: Ids.Agent2), Sample().All));
        Assert.Empty(Run(Spec(TicketQueueKind.Mine, me: Guid.Empty), Sample().All));
    }

    [Fact]
    public void UnassignedKeepsWhatHasNoAssignee()
    {
        Assert.Equal(["T-000001"], Run(Spec(TicketQueueKind.Unassigned), Sample().All));
    }

    [Fact]
    public void AnExplicitStatusListReplacesTheWorkingDefault()
    {
        Ticket[] all = Sample().All;

        Assert.Equal(["T-000004"], Run(Spec(TicketQueueKind.Unassigned, statuses: [TicketStatus.Resolved]), all));
        Assert.Equal(["T-000003"], Run(Spec(TicketQueueKind.All, statuses: [TicketStatus.OnHold]), all));
    }

    [Fact]
    public void TheWorkingStatusesAreNewOpenPendingCustomerAndOnHold()
    {
        Assert.Equal(
            [TicketStatus.New, TicketStatus.Open, TicketStatus.PendingCustomer, TicketStatus.OnHold],
            TicketSearchSpecification.WorkingStatuses);
    }

    [Fact]
    public void PrioritiesNarrowTheResult()
    {
        Assert.Equal(["T-000003", "T-000002"], Run(Spec(priorities: [TicketPriority.High, TicketPriority.Urgent]), Sample().All));
    }

    [Fact]
    public void TheAssigneeFilterKeepsThatAgentsTickets()
    {
        Assert.Equal(["T-000003"], Run(Spec(assignee: Ids.Agent2), Sample().All));
    }

    [Fact]
    public void TheCategoryFilterKeepsThatCategory()
    {
        (Ticket[] all, Category category) = Sample();

        Assert.Equal(["T-000003"], Run(Spec(category: category.Id.Value), all));
    }

    [Fact]
    public void TheDepartmentAndBranchFiltersKeepThatPlace()
    {
        Ticket[] all = Sample().All;

        Assert.Equal(["T-000003"], Run(Spec(department: Ids.DepartmentA2.Value), all));
        Assert.Equal(["T-000004", "T-000002", "T-000001"], Run(Spec(department: Ids.DepartmentA1.Value), all));
        Assert.Equal(["T-000003"], Run(Spec(branch: Ids.BranchA2.Value), all));
    }

    [Fact]
    public void TheCustomerFilterKeepsThatCustomer()
    {
        Assert.Equal(["T-000002"], Run(Spec(customer: Ids.Customer2), Sample().All));
    }

    [Theory]
    [InlineData("Printer", new[] { "T-000001" })]
    [InlineData("  Printer  ", new[] { "T-000001" })]
    [InlineData("T-000003", new[] { "T-000003" })]
    [InlineData("problem", new[] { "T-000003" })]
    [InlineData("", new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData("  ", new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData(null, new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    public void TheSearchMatchesNumberOrSubjectAndABlankOneMatchesAll(string? search, string[] expected)
    {
        Assert.Equal(expected, Run(Spec(search: search), Sample().All));
    }

    [Theory]
    [InlineData(TicketSort.CreatedAt, true, new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData(TicketSort.CreatedAt, false, new[] { "T-000001", "T-000002", "T-000003", "T-000004" })]
    [InlineData(TicketSort.ModifiedAt, true, new[] { "T-000004", "T-000002", "T-000003", "T-000001" })]
    [InlineData(TicketSort.ModifiedAt, false, new[] { "T-000001", "T-000003", "T-000002", "T-000004" })]
    [InlineData(TicketSort.Priority, true, new[] { "T-000003", "T-000002", "T-000001", "T-000004" })]
    [InlineData(TicketSort.Priority, false, new[] { "T-000004", "T-000001", "T-000002", "T-000003" })]
    [InlineData(TicketSort.Number, true, new[] { "T-000004", "T-000003", "T-000002", "T-000001" })]
    [InlineData(TicketSort.Number, false, new[] { "T-000001", "T-000002", "T-000003", "T-000004" })]
    public void EachSortKeyOrdersInBothDirections(TicketSort sort, bool descending, string[] expected)
    {
        Assert.Equal(expected, Run(Spec(sort: sort, descending: descending), Sample().All));
    }

    [Fact]
    public void TheNumberBreaksTiesAscending()
    {
        Ticket x = TicketFixtures.Create(7, priority: TicketPriority.High).Stamp(Clock.Now, null);
        Ticket y = TicketFixtures.Create(5, priority: TicketPriority.High).Stamp(Clock.Now, null);
        Ticket z = TicketFixtures.Create(6, priority: TicketPriority.High).Stamp(Clock.Now, null);

        Assert.Equal(["T-000005", "T-000006", "T-000007"], Run(Spec(sort: TicketSort.Priority), [x, y, z]));
        Assert.Equal(["T-000005", "T-000006", "T-000007"], Run(Spec(sort: TicketSort.CreatedAt, descending: false), [x, y, z]));
    }

    [Theory]
    [InlineData(1, 20, 0, 20)]
    [InlineData(3, 10, 20, 10)]
    [InlineData(2, 1, 1, 1)]
    public void PagingIsAppliedWhenBothArgumentsAreGiven(int page, int size, int skip, int take)
    {
        TicketSearchSpecification spec = Spec(page: page, pageSize: size);

        Assert.Equal(skip, spec.Skip);
        Assert.Equal(take, spec.Take);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(2, null)]
    [InlineData(null, 5)]
    public void NoPagingWithoutBothArguments(int? page, int? size)
    {
        TicketSearchSpecification spec = Spec(page: page, pageSize: size);

        Assert.Null(spec.Skip);
        Assert.Null(spec.Take);
    }

    [Fact]
    public void ThePageIsActuallyCutWhenEvaluated()
    {
        Assert.Equal(["T-000002", "T-000001"], Run(Spec(page: 2, pageSize: 2), Sample().All));
    }

    [Fact]
    public void ASpecificationIsAlwaysReadOnlyAndNeverLiftsAFilter()
    {
        TicketSearchSpecification spec = Spec();

        Assert.True(spec.AsNoTracking);
        Assert.False(spec.IgnoreOrganisationScopeFilter);
        Assert.False(spec.IgnoreSoftDeleteFilter);
    }

    [Fact]
    public void ANullStatusOrPriorityListIsRefused()
    {
        Func<TicketQueueKind, Guid, string?, IReadOnlyList<TicketStatus>, IReadOnlyList<TicketPriority>, Guid?, Guid?, Guid?, Guid?, Guid?, TicketSort, bool, int?, int?, TicketSearchSpecification> create =
            (q, me, s, st, pr, c, a, d, b, cu, so, de, p, z) => new TicketSearchSpecification(q, me, s, st, pr, c, a, d, b, cu, so, de, p, z);

        NullArguments.AssertThrowsArgumentNull(create, TicketQueueKind.All, Ids.Actor, null, null, NoPriorities, null, null, null, null, null, TicketSort.CreatedAt, true, null, null);
        NullArguments.AssertThrowsArgumentNull(create, TicketQueueKind.All, Ids.Actor, null, NoStatuses, null, null, null, null, null, null, TicketSort.CreatedAt, true, null, null);
    }
}
