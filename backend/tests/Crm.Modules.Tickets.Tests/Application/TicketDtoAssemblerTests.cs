using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application;

public sealed class TicketDtoAssemblerTests
{
    private readonly Harness _h = new();

    private static bool SameIds(IReadOnlyCollection<Guid> actual, params Guid[] expected) =>
        actual.Count == expected.Length && expected.All(actual.Contains);

    // ---- ToDtoAsync ----

    [Fact]
    public async Task ActivitiesAreOrderedBySequence()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        ticket.PutOnHold(Ids.Actor, Clock.Now);
        ticket.Resolve(Ids.Actor, Clock.Now);

        TicketDto dto = await _h.Assembler().ToDtoAsync(ticket, Stubs.Ct);

        Assert.Equal(["Created", "Opened", "PutOnHold", "Resolved"], dto.Activities.Select(a => a.Kind));
    }

    [Fact]
    public async Task PersonIdsAreTheAssigneeEveryActorAndTheGuidsOfAssignmentActivitiesOnly()
    {
        Ticket ticket = TicketFixtures.Create();
        ticket.Open(Ids.Agent1, Clock.Now);
        ticket.AssignTo(Ids.Agent2, Ids.Contact1, Clock.Now);
        ticket.Unassign(Ids.Contact2, Clock.Now);
        ticket.Claim(Ids.Missing, Clock.Now);

        await _h.Assembler().ToDtoAsync(ticket, Stubs.Ct);

        // Actors: Actor, Agent1, Contact1, Contact2, Missing; assignment From/To: Agent2 (To of Assigned), Agent2 (From of Unassigned).
        await _h.Identity.Received(1).GetStaffUsersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => SameIds(ids, Ids.Actor, Ids.Agent1, Ids.Agent2, Ids.Contact1, Ids.Contact2, Ids.Missing)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StatusNamesAndDepartmentIdsOfOtherActivitiesAreNotLookedUp()
    {
        Ticket ticket = TicketFixtures.Create();
        ticket.Open(Ids.Actor, Clock.Now);
        ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Now);

        await _h.Assembler().ToDtoAsync(ticket, Stubs.Ct);

        await _h.Identity.Received(1).GetStaffUsersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => SameIds(ids, Ids.Actor)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheCategoryIsReadThroughGetByIdOnlyWhenTheTicketHasOne()
    {
        Ticket without = TicketFixtures.Create();
        await _h.Assembler().ToDtoAsync(without, Stubs.Ct);
        await _h.Categories.DidNotReceive().GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>());

        Category category = TicketFixtures.NewCategory();
        _h.Holding(category);
        Ticket with = TicketFixtures.Create(category: category);

        TicketDto dto = await _h.Assembler().ToDtoAsync(with, Stubs.Ct);

        await _h.Categories.Received(1).GetByIdAsync(category.Id, Arg.Any<CancellationToken>());
        Assert.Equal(category.Id.Value, dto.Category?.Id);
    }

    [Fact]
    public async Task ACategoryTheRepositoryCannotFindLeavesTheRefNull()
    {
        Category category = TicketFixtures.NewCategory();
        Ticket ticket = TicketFixtures.Create(category: category);

        TicketDto dto = await _h.Assembler().ToDtoAsync(ticket, Stubs.Ct);

        Assert.Null(dto.Category);
    }

    [Fact]
    public async Task AnIdentityFailureGivesNoNamesAndNoPeople()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);
        _h.Quietly(() => _h.Identity.GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IReadOnlyList<StaffUserSummary>>(Error.Forbidden("Organisation.OutOfScope", "x")))));

        TicketDto dto = await _h.Assembler().ToDtoAsync(ticket, Stubs.Ct);

        Assert.Null(dto.AssignedAgentName);
        Assert.Empty(dto.People);
    }

    [Fact]
    public async Task TheDtoCopiesTheRowVersionAndTheAllowedActions()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketDto dto = await _h.Assembler().ToDtoAsync(ticket, Stubs.Ct);

        Assert.Same(ticket.RowVersion, dto.RowVersion);
        Assert.Equal(ticket.AllowedActions().Select(a => a.ToString()), dto.AllowedActions);
    }

    [Fact]
    public async Task ANullTicketIsRefused()
    {
        TicketDtoAssembler assembler = _h.Assembler();

        await NullArguments.AssertThrowsArgumentNullAsync(assembler.ToDtoAsync, null, Stubs.Ct);
    }

    // ---- ToThreadPageAsync ----

    [Fact]
    public async Task TheNextCursorIsTheLastItemsSequenceWhenMoreRowsThanTheLimitCame()
    {
        TicketThreadRow[] rows = [Rows.Message(1), Rows.Message(2), Rows.Message(3)];

        TicketThreadPageDto page = await _h.Assembler().ToThreadPageAsync(rows, 2, Stubs.Ct);

        Assert.Equal([1, 2], page.Items.Select(i => i.Sequence));
        Assert.Equal(2, page.NextCursor);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task ThereIsNoNextCursorWhenTheRowsFitTheLimit(int limit)
    {
        TicketThreadRow[] rows = [Rows.Message(1), Rows.Message(2)];

        TicketThreadPageDto page = await _h.Assembler().ToThreadPageAsync(rows, limit, Stubs.Ct);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task EntriesCarryTheirTypeVisibilityDirectionAndBody()
    {
        Guid messageId = Guid.NewGuid();
        Guid noteId = Guid.NewGuid();
        Guid activityId = Guid.NewGuid();
        TicketThreadRow[] rows =
        [
            Rows.Message(1, TicketMessageDirection.Inbound, Ids.Contact1, Ids.Agent1, "Hi", messageId),
            Rows.Note(2, Ids.Agent1, "Check", noteId),
            Rows.Activity(3, TicketActivityKind.PriorityChanged, Ids.Agent1, "Normal", "High", activityId),
        ];

        TicketThreadPageDto page = await _h.Assembler().ToThreadPageAsync(rows, 10, Stubs.Ct);

        TicketThreadEntryDto message = page.Items[0];
        Assert.Equal(("Message", "Public", "Inbound", "Hi", messageId), (message.Type, message.Visibility, message.Direction, message.Body, message.Id));
        Assert.Equal(Ids.Contact1, message.AuthorId);
        Assert.Equal(Ids.Agent1, message.RecordedById);
        Assert.Null(message.TemplateKey);
        Assert.Null(message.Parameters);

        TicketThreadEntryDto note = page.Items[1];
        Assert.Equal(("Note", "Internal", "Check", noteId), (note.Type, note.Visibility, note.Body, note.Id));
        Assert.Null(note.Direction);
        Assert.Null(note.Parameters);

        TicketThreadEntryDto activity = page.Items[2];
        Assert.Equal(("Activity", "System", "PriorityChanged", activityId), (activity.Type, activity.Visibility, activity.TemplateKey, activity.Id));
        Assert.Null(activity.Body);
        Assert.Equal("Normal", activity.Parameters?["from"]);
        Assert.Equal("High", activity.Parameters?["to"]);
        Assert.Equal(Clock.Later(3), activity.OccurredAtUtc);
    }

    [Fact]
    public async Task OutboundAuthorsAndRecordersAreNamedButInboundAuthorsAreNotLookedUpAsStaff()
    {
        TicketThreadRow[] rows =
        [
            Rows.Message(1, TicketMessageDirection.Outbound, Ids.Agent1, Ids.Agent1),
            Rows.Message(2, TicketMessageDirection.Inbound, Ids.Contact1, Ids.Agent2),
            Rows.Note(3, Ids.Actor),
        ];

        await _h.Assembler().ToThreadPageAsync(rows, 10, Stubs.Ct);

        await _h.Identity.Received(1).GetStaffUsersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => SameIds(ids, Ids.Agent1, Ids.Agent2, Ids.Actor)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignmentActivityGuidsAreNamedAndOtherKindsAreNot()
    {
        TicketThreadRow[] rows =
        [
            Rows.Activity(1, TicketActivityKind.Assigned, Ids.Actor, null, Ids.Agent1.ToString()),
            Rows.Activity(2, TicketActivityKind.Claimed, Ids.Actor, null, Ids.Agent2.ToString()),
            Rows.Activity(3, TicketActivityKind.Unassigned, Ids.Actor, Ids.Contact1.ToString(), null),
            Rows.Activity(4, TicketActivityKind.PriorityChanged, Ids.Actor, Ids.Contact2.ToString(), Ids.Missing.ToString()),
            Rows.Activity(5, TicketActivityKind.Opened, Ids.Actor, "New", "Open"),
        ];

        await _h.Assembler().ToThreadPageAsync(rows, 10, Stubs.Ct);

        await _h.Identity.Received(1).GetStaffUsersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => SameIds(ids, Ids.Actor, Ids.Agent1, Ids.Agent2, Ids.Contact1)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThePageNamesPeopleFromIdentity()
    {
        _h.KnowsStaff(TicketFixtures.Staff(Ids.Agent1, "Agent One"));

        TicketThreadPageDto page = await _h.Assembler().ToThreadPageAsync([Rows.Message(1, TicketMessageDirection.Outbound, Ids.Agent1, Ids.Agent1)], 10, Stubs.Ct);

        Assert.Equal(new TicketPersonDto(Ids.Agent1, "Agent One"), Assert.Single(page.People));
    }

    [Fact]
    public async Task NoStaffIdsMeansNoIdentityCall()
    {
        TicketThreadRow[] rows = [Rows.Message(1, TicketMessageDirection.Inbound, Ids.Contact1, null)];

        await _h.Assembler().ToThreadPageAsync(rows, 10, Stubs.Ct);

        await _h.Identity.DidNotReceive().GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CategoryChangedIdsAreResolvedWithOneByIdsRead()
    {
        Category first = TicketFixtures.NewCategory(en: "First");
        Category second = TicketFixtures.NewCategory(en: "Second");
        _h.Quietly(() => _h.Categories.Lists(first, second));
        TicketThreadRow[] rows =
        [
            Rows.Activity(1, TicketActivityKind.CategoryChanged, Ids.Actor, first.Id.Value.ToString(), second.Id.Value.ToString()),
            Rows.Activity(2, TicketActivityKind.CategoryChanged, Ids.Actor, second.Id.Value.ToString(), first.Id.Value.ToString()),
        ];

        TicketThreadPageDto page = await _h.Assembler().ToThreadPageAsync(rows, 10, Stubs.Ct);

        await _h.Categories.Received(1).ListAsync(Arg.Is<Specification<Category>>(s => s is CategoriesByIdsSpecification), Arg.Any<CancellationToken>());
        Assert.Equal(["First", "Second"], page.Categories.Select(c => c.Name.En).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task NoCategoryChangedRowsMeansNoCategoryRead()
    {
        await _h.Assembler().ToThreadPageAsync([Rows.Message(1), Rows.Note(2, Ids.Actor)], 10, Stubs.Ct);

        await _h.Categories.DidNotReceive().ListAsync(Arg.Any<Specification<Category>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransferredIdsAreResolvedOneDepartmentAtATimeAndFailuresAreSkipped()
    {
        _h.KnowsDepartments(TicketFixtures.Department(Ids.DepartmentA2, code: "A2", nameAr: "ب", nameEn: "Second"));
        TicketThreadRow[] rows =
        [
            Rows.Activity(1, TicketActivityKind.Transferred, Ids.Actor, Ids.DepartmentA1.Value.ToString(), Ids.DepartmentA2.Value.ToString()),
        ];

        TicketThreadPageDto page = await _h.Assembler().ToThreadPageAsync(rows, 10, Stubs.Ct);

        await _h.Organisation.Received(2).GetDepartmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        TicketDepartmentRefDto department = Assert.Single(page.Departments);
        Assert.Equal(new TicketDepartmentRefDto(Ids.DepartmentA2.Value, "A2", "ب", "Second"), department);
    }

    [Fact]
    public async Task StaffIdsAreCappedAtTwoHundredPerCall()
    {
        TicketThreadRow[] rows = [.. Enumerable.Range(1, 250).Select(n => Rows.Note(n, Guid.NewGuid()))];

        await _h.Assembler().ToThreadPageAsync(rows, 250, Stubs.Ct);

        await _h.Identity.Received(1).GetStaffUsersAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 200), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailuresNeverFailThePage()
    {
        _h.Quietly(() =>
        {
            _h.Identity.GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Failure<IReadOnlyList<StaffUserSummary>>(Error.Forbidden("Organisation.OutOfScope", "x"))));
            _h.Organisation.GetDepartmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Failure<DepartmentReference>(Error.NotFound("Organisation.DepartmentNotFound", "x"))));
        });
        TicketThreadRow[] rows =
        [
            Rows.Note(1, Ids.Agent1),
            Rows.Activity(2, TicketActivityKind.Transferred, Ids.Agent1, Ids.DepartmentA1.Value.ToString(), Ids.DepartmentA2.Value.ToString()),
        ];

        TicketThreadPageDto page = await _h.Assembler().ToThreadPageAsync(rows, 10, Stubs.Ct);

        Assert.Equal(2, page.Items.Count);
        Assert.Empty(page.People);
        Assert.Empty(page.Departments);
    }

    [Fact]
    public async Task ANullRowListIsRefused()
    {
        TicketDtoAssembler assembler = _h.Assembler();

        await NullArguments.AssertThrowsArgumentNullAsync(assembler.ToThreadPageAsync, null, 10, Stubs.Ct);
    }

    // ---- ToListItemsAsync ----

    [Fact]
    public async Task ListItemsLookUpCategoriesAgentsAndCustomersOnceForTheDistinctIds()
    {
        Category category = TicketFixtures.NewCategory();
        _h.Quietly(() => _h.Categories.Lists(category));
        TicketListRow[] rows =
        [
            Rows.List(Ids.Ticket1, "T-000001", category: category.Id.Value, agent: Ids.Agent1),
            Rows.List(Ids.Ticket2, "T-000002", category: category.Id.Value, agent: Ids.Agent1),
            Rows.List(Ids.Ticket3, "T-000003", customer: Ids.Customer2),
        ];

        IReadOnlyList<TicketListItemDto> items = await _h.Assembler().ToListItemsAsync(rows, Stubs.Ct);

        Assert.Equal(3, items.Count);
        await _h.Categories.Received(1).ListAsync(Arg.Any<Specification<Category>>(), Arg.Any<CancellationToken>());
        await _h.Identity.Received(1).GetStaffUsersAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => SameIds(ids, Ids.Agent1)), Arg.Any<CancellationToken>());
        await _h.Customers.Received(1).GetCustomerNamesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => SameIds(ids, Ids.Customer1, Ids.Customer2)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListItemsWithNoCategoriesOrAgentsSkipThoseLookups()
    {
        IReadOnlyList<TicketListItemDto> items = await _h.Assembler().ToListItemsAsync([Rows.List()], Stubs.Ct);

        Assert.Single(items);
        await _h.Categories.DidNotReceive().ListAsync(Arg.Any<Specification<Category>>(), Arg.Any<CancellationToken>());
        await _h.Identity.DidNotReceive().GetStaffUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyRowsMakeNoLookupsAtAll()
    {
        IReadOnlyList<TicketListItemDto> items = await _h.Assembler().ToListItemsAsync([], Stubs.Ct);

        Assert.Empty(items);
        Assert.DoesNotContain("lookup", _h.Log);
    }

    [Fact]
    public async Task ANullRowListIsRefusedForListItemsToo()
    {
        TicketDtoAssembler assembler = _h.Assembler();

        await NullArguments.AssertThrowsArgumentNullAsync(assembler.ToListItemsAsync, null, Stubs.Ct);
    }

    [Fact]
    public async Task AnInactiveCategoryStillShowsItsNameWithTheInactiveFlag()
    {
        Category category = TicketFixtures.NewCategory();
        Ticket ticket = TicketFixtures.Create(category: category);
        category.Deactivate();
        _h.Holding(category);

        TicketDto dto = await _h.Assembler().ToDtoAsync(ticket, Stubs.Ct);

        Assert.False(dto.Category?.IsActive);
        Assert.Equal("General enquiry", dto.Category?.Name.En);
    }

    [Fact]
    public async Task CustomerNamesAreMatchedToTheirRows()
    {
        _h.Quietly(() => _h.Customers.GetCustomerNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<CustomerNameSummary>>(
                [new CustomerNameSummary(Ids.Customer2, "C-000002", "ب", "Bee")]))));
        TicketListRow[] rows = [Rows.List(Ids.Ticket1, customer: Ids.Customer1), Rows.List(Ids.Ticket2, "T-000002", customer: Ids.Customer2)];

        IReadOnlyList<TicketListItemDto> items = await _h.Assembler().ToListItemsAsync(rows, Stubs.Ct);

        Assert.Null(items[0].Customer);
        Assert.Equal(new TicketCustomerRefDto(Ids.Customer2, "C-000002", "ب", "Bee"), items[1].Customer);
    }
}
