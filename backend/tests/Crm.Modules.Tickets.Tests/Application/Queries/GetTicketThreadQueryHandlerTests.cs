using Crm.BuildingBlocks.Application.Exceptions;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Queries.GetTicketThread;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class GetTicketThreadQueryHandlerTests
{
    private readonly Harness _h = new();
    private readonly ITicketThreadReadModel _threads = Substitute.For<ITicketThreadReadModel>();
    private readonly TicketId _id = new(Ids.Ticket1);

    private GetTicketThreadQueryHandler Handler() => new(_threads, _h.Tickets, _h.Assembler());

    private static GetTicketThreadQuery Query(IReadOnlyList<string>? visibility = null, int? after = null, int limit = 50) =>
        new(Ids.Ticket1, after, limit, visibility ?? []);

    private void Returns(IReadOnlyList<TicketThreadRow>? rows)
    {
        _threads.ListAsync(Arg.Any<TicketId>(), Arg.Any<IReadOnlyCollection<TicketThreadVisibility>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(rows));
        _threads.ListPublicAsync(Arg.Any<TicketId>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(rows));
    }

    [Fact]
    public async Task NoVisibilityAsksForAllThreeThroughListAsync()
    {
        Returns([]);

        Result<TicketThreadPageDto> result = await Handler().Handle(Query(), Stubs.Ct);

        Expect.Success(result);
        await _threads.Received(1).ListAsync(
            _id,
            Arg.Is<IReadOnlyCollection<TicketThreadVisibility>>(v => v.SequenceEqual(new[]
            {
                TicketThreadVisibility.Public, TicketThreadVisibility.Internal, TicketThreadVisibility.System,
            })),
            0, 51, Arg.Any<CancellationToken>());
        await _threads.DidNotReceive().ListPublicAsync(Arg.Any<TicketId>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("public")]
    [InlineData("Public")]
    [InlineData("PUBLIC")]
    public async Task PublicOnlyUsesListPublicAsyncAndNeverListAsync(string visibility)
    {
        Returns([]);

        await Handler().Handle(Query([visibility]), Stubs.Ct);

        await _threads.Received(1).ListPublicAsync(_id, 0, 51, Arg.Any<CancellationToken>());
        await _threads.DidNotReceive().ListAsync(
            Arg.Any<TicketId>(), Arg.Any<IReadOnlyCollection<TicketThreadVisibility>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepeatedPublicInAnyCaseIsDeduplicatedToPublicOnly()
    {
        Returns([]);

        await Handler().Handle(Query(["Public", "public"]), Stubs.Ct);

        await _threads.Received(1).ListPublicAsync(_id, 0, 51, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublicAndInternalUseListAsyncWithBoth()
    {
        Returns([]);

        await Handler().Handle(Query(["public", "internal"]), Stubs.Ct);

        await _threads.Received(1).ListAsync(
            _id,
            Arg.Is<IReadOnlyCollection<TicketThreadVisibility>>(v => v.SequenceEqual(new[] { TicketThreadVisibility.Public, TicketThreadVisibility.Internal })),
            0, 51, Arg.Any<CancellationToken>());
        await _threads.DidNotReceive().ListPublicAsync(Arg.Any<TicketId>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("internal", TicketThreadVisibility.Internal)]
    [InlineData("system", TicketThreadVisibility.System)]
    public async Task ASingleStaffVisibilityUsesListAsync(string visibility, TicketThreadVisibility expected)
    {
        Returns([]);

        await Handler().Handle(Query([visibility]), Stubs.Ct);

        await _threads.Received(1).ListAsync(
            _id, Arg.Is<IReadOnlyCollection<TicketThreadVisibility>>(v => v.SequenceEqual(new[] { expected })), 0, 51, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    public async Task TheCursorDefaultsToZero(int? after, int expected)
    {
        Returns([]);

        await Handler().Handle(Query(after: after), Stubs.Ct);

        await _threads.Received(1).ListAsync(
            _id, Arg.Any<IReadOnlyCollection<TicketThreadVisibility>>(), expected, 51, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(50, 51)]
    [InlineData(100, 101)]
    public async Task OneExtraRowIsRequestedToDetectANextPage(int limit, int take)
    {
        Returns([]);

        await Handler().Handle(Query(limit: limit), Stubs.Ct);

        await _threads.Received(1).ListAsync(
            _id, Arg.Any<IReadOnlyCollection<TicketThreadVisibility>>(), 0, take, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANullPageAsksTheRepositoryOnceAndAnUnknownTicketIsNotFound()
    {
        Returns(null);

        Result<TicketThreadPageDto> result = await Handler().Handle(Query(), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        await _h.Tickets.Received(1).GetByIdAsync(_id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnOutOfScopeTicketPropagatesTheScopeViolation()
    {
        Returns(null);
        _h.Tickets.GetByIdAsync(Arg.Any<TicketId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Ticket?>(new OrganisationScopeViolationException("Ticket", "Read")));

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(() => Handler().Handle(Query(), Stubs.Ct));
    }

    [Fact]
    public async Task ASuccessfulPageIsBuiltByTheAssembler()
    {
        Returns([Rows.Message(1, TicketMessageDirection.Outbound, Ids.Agent1, Ids.Agent1), Rows.Note(2, Ids.Agent1)]);
        _h.KnowsStaff(TicketFixtures.Staff(Ids.Agent1, "Agent One"));

        Result<TicketThreadPageDto> result = await Handler().Handle(Query(), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(["Message", "Note"], result.Value.Items.Select(i => i.Type));
        Assert.Equal("Agent One", Assert.Single(result.Value.People).DisplayName);
        Assert.Null(result.Value.NextCursor);
        await _h.Tickets.DidNotReceive().GetByIdAsync(Arg.Any<TicketId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnUnparsableVisibilityThatBypassedTheValidatorThrowsArgumentException()
    {
        Returns([]);

        await Assert.ThrowsAsync<ArgumentException>(() => Handler().Handle(Query(["private"]), Stubs.Ct));
    }
}
