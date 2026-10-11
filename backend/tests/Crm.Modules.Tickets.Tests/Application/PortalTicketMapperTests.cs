using System.Reflection;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application;

public sealed class PortalTicketMapperTests
{
    private readonly Harness _h = new();

    [Fact]
    public async Task SummariesCarryThePortalStatusAndTheUpdatedTimestamp()
    {
        PortalTicketRow[] rows =
        [
            Rows.Portal(Ids.Ticket1, "T-000001", status: TicketStatus.PendingCustomer, created: Clock.Now, modified: Clock.Later(5)),
            Rows.Portal(Ids.Ticket2, "T-000002", status: TicketStatus.New, created: Clock.Later(2)),
        ];

        IReadOnlyList<PortalTicketSummaryDto> summaries = await _h.Mapper().ToSummariesAsync(rows, Stubs.Ct);

        Assert.Equal("AwaitingYourReply", summaries[0].Status);
        Assert.Equal(Clock.Later(5), summaries[0].UpdatedAtUtc);
        Assert.Equal(Clock.Now, summaries[0].CreatedAtUtc);
        Assert.Equal("Received", summaries[1].Status);
        Assert.Equal(Clock.Later(2), summaries[1].UpdatedAtUtc);
        Assert.Equal(("T-000001", "Printer is down"), (summaries[0].Number, summaries[0].Subject));
    }

    [Fact]
    public async Task SummariesReadCategoryNamesThroughOneByIdsRead()
    {
        Category category = TicketFixtures.NewCategory(ar: "تقنية", en: "Technical");
        _h.Quietly(() => _h.Categories.Lists(category));
        PortalTicketRow[] rows = [Rows.Portal(category: category.Id.Value), Rows.Portal(Ids.Ticket2, category: category.Id.Value), Rows.Portal(Ids.Ticket3)];

        IReadOnlyList<PortalTicketSummaryDto> summaries = await _h.Mapper().ToSummariesAsync(rows, Stubs.Ct);

        await _h.Categories.Received(1).ListAsync(Arg.Is<Specification<Category>>(s => s is CategoriesByIdsSpecification), Arg.Any<CancellationToken>());
        Assert.Equal(new PortalTicketCategoryDto(category.Id.Value, "تقنية", "Technical"), summaries[0].Category);
        Assert.Equal(summaries[0].Category, summaries[1].Category);
        Assert.Null(summaries[2].Category);
    }

    [Fact]
    public async Task NoCategoryIdsMeansNoCategoryRead()
    {
        await _h.Mapper().ToSummariesAsync([Rows.Portal()], Stubs.Ct);
        await _h.Mapper().ToDtoAsync(Rows.Portal(), Stubs.Ct);
        await _h.Mapper().ToDtoAsync(TicketFixtures.Create(), Stubs.Ct);

        await _h.Categories.DidNotReceive().ListAsync(Arg.Any<Specification<Category>>(), Arg.Any<CancellationToken>());
        await _h.Categories.DidNotReceive().GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ARowAndTheTicketItWasProjectedFromGiveTheSameDto()
    {
        Category category = TicketFixtures.NewCategory();
        _h.Quietly(() => _h.Categories.Lists(category));
        Ticket ticket = TicketFixtures.Create(category: category, contact: Ids.Contact1, id: new TicketId(Ids.Ticket1));
        ticket.Open(Ids.Agent1, Clock.Later(1));
        ticket.Resolve(Ids.Agent1, Clock.Later(2));
        ticket.Stamp(Clock.Now, Clock.Later(3));
        PortalTicketRow row = Rows.Portal(
            Ids.Ticket1, "T-000001", ticket.Subject, ticket.Description, TicketStatus.Resolved, category.Id.Value, Ids.Contact1, Clock.Now,
            Clock.Later(3), Clock.Later(2), null);

        PortalTicketDto fromRow = await _h.Mapper().ToDtoAsync(row, Stubs.Ct);
        PortalTicketDto fromTicket = await _h.Mapper().ToDtoAsync(ticket, Stubs.Ct);

        Assert.Equal(fromRow.Id, fromTicket.Id);
        Assert.Equal(fromRow.Number, fromTicket.Number);
        Assert.Equal(fromRow.Subject, fromTicket.Subject);
        Assert.Equal(fromRow.Description, fromTicket.Description);
        Assert.Equal(fromRow.Status, fromTicket.Status);
        Assert.Equal(fromRow.Category, fromTicket.Category);
        Assert.Equal(fromRow.CreatedAtUtc, fromTicket.CreatedAtUtc);
        Assert.Equal(fromRow.UpdatedAtUtc, fromTicket.UpdatedAtUtc);
        Assert.Equal(fromRow.ResolvedAtUtc, fromTicket.ResolvedAtUtc);
        Assert.Equal(fromRow.ClosedAtUtc, fromTicket.ClosedAtUtc);
        Assert.Equal(fromRow.Actions, fromTicket.Actions);
    }

    [Theory]
    [InlineData(TicketStatus.New, new[] { "Reply", "RequestClosure" })]
    [InlineData(TicketStatus.Resolved, new[] { "Reply", "Reopen" })]
    [InlineData(TicketStatus.Closed, new string[0])]
    public async Task ActionsAreTheCustomerActionsOfTheStatus(TicketStatus status, string[] expected)
    {
        PortalTicketDto fromRow = await _h.Mapper().ToDtoAsync(Rows.Portal(status: status), Stubs.Ct);
        PortalTicketDto fromTicket = await _h.Mapper().ToDtoAsync(TicketFixtures.InStatus(status), Stubs.Ct);

        Assert.Equal(expected, fromRow.Actions);
        Assert.Equal(expected, fromTicket.Actions);
        Assert.Equal(Ticket.CustomerActionsFor(status).Select(a => a.ToString()), fromRow.Actions);
    }

    [Fact]
    public async Task TheUpdatedTimestampFallsBackToTheCreatedOne()
    {
        PortalTicketDto fromRow = await _h.Mapper().ToDtoAsync(Rows.Portal(created: Clock.Later(4)), Stubs.Ct);
        PortalTicketDto fromTicket = await _h.Mapper().ToDtoAsync(TicketFixtures.Create().Stamp(Clock.Later(4), null), Stubs.Ct);

        Assert.Equal(Clock.Later(4), fromRow.UpdatedAtUtc);
        Assert.Equal(Clock.Later(4), fromTicket.UpdatedAtUtc);
    }

    [Fact]
    public async Task ANullArgumentIsRefused()
    {
        PortalTicketMapper mapper = _h.Mapper();

        await NullArguments.AssertThrowsArgumentNullAsync(mapper.ToSummariesAsync, null, Stubs.Ct);
        await NullArguments.AssertThrowsArgumentNullAsync((Func<PortalTicketRow, CancellationToken, Task<PortalTicketDto>>)mapper.ToDtoAsync, null, Stubs.Ct);
        await NullArguments.AssertThrowsArgumentNullAsync((Func<Ticket, CancellationToken, Task<PortalTicketDto>>)mapper.ToDtoAsync, null, Stubs.Ct);
    }

    public static TheoryData<Type> PortalDtoTypes() =>
    [
        typeof(PortalTicketDto), typeof(PortalTicketSummaryDto), typeof(PortalThreadMessageDto), typeof(PortalThreadPageDto), typeof(PortalTicketCategoryDto),
    ];

    private static readonly string[] Forbidden =
        ["assign", "department", "branch", "priority", "rowversion", "note", "activit", "agent", "staff", "recordedby", "authorid", "channel", "tenant", "customerid", "contactid"];

    [Theory]
    [MemberData(nameof(PortalDtoTypes))]
    public void NoPortalDtoHasAMemberThatCouldLeakInternalData(Type dto)
    {
        IEnumerable<string> names = dto.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name.ToLowerInvariant());

        foreach (string name in names)
        {
            Assert.DoesNotContain(Forbidden, word => name.Contains(word, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ThePortalRowRecordsCarryNoInternalFieldEither()
    {
        IEnumerable<string> names = typeof(PortalTicketRow).GetProperties().Select(p => p.Name.ToLowerInvariant());

        foreach (string name in names.Where(n => n != "contactid"))
        {
            Assert.DoesNotContain(Forbidden, word => name.Contains(word, StringComparison.Ordinal));
        }
    }
}
