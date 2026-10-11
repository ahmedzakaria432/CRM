using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Queries.GetPortalTicket;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class GetPortalTicketQueryHandlerTests
{
    private readonly IPortalTicketReadModel _readModel = Substitute.For<IPortalTicketReadModel>();

    private GetPortalTicketQueryHandler Handler(Harness h) => new(_readModel, h.User, h.Mapper());

    private void Returns(params PortalTicketRow[] rows) =>
        _readModel.ListAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<PortalTicketRow>>(rows));

    private static Harness Portal() => new(user: CurrentUsers.Portal(Scopes.Grant()));

    [Fact]
    public async Task ANonPortalCallerIsUnauthorized()
    {
        Result<PortalTicketDto> result = await Handler(new Harness()).Handle(new GetPortalTicketQuery(Ids.Ticket1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.PortalOnly, ErrorType.Unauthorized);
        await _readModel.DidNotReceive().ListAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheReadModelIsAskedWithAReadOnlyPortalSpecificationAndTheRepositoryIsNeverUsed()
    {
        Harness h = Portal();
        Returns(Rows.Portal());

        await Handler(h).Handle(new GetPortalTicketQuery(Ids.Ticket1), Stubs.Ct);

        await _readModel.Received(1).ListAsync(
            Arg.Is<Specification<Ticket>>(s => s is PortalTicketsSpecification && s.AsNoTracking), Arg.Any<CancellationToken>());
        await h.Tickets.DidNotReceive().GetByIdAsync(Arg.Any<TicketId>(), Arg.Any<CancellationToken>());
        await h.Tickets.DidNotReceive().ListAsync(Arg.Any<Specification<Ticket>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoRowIsNotFound()
    {
        Returns();

        Result<PortalTicketDto> result = await Handler(Portal()).Handle(new GetPortalTicketQuery(Ids.Ticket1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
    }

    [Fact]
    public async Task AFoundRowIsMappedToThePortalDto()
    {
        Returns(Rows.Portal(Ids.Ticket1, "T-000009", "Printer", "Broken", TicketStatus.OnHold, null, Ids.Contact1, Clock.Now, Clock.Later(1)));

        Result<PortalTicketDto> result = await Handler(Portal()).Handle(new GetPortalTicketQuery(Ids.Ticket1), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(("T-000009", "Printer", "Broken", "InProgress"), (result.Value.Number, result.Value.Subject, result.Value.Description, result.Value.Status));
        Assert.Equal(["Reply", "RequestClosure"], result.Value.Actions);
    }
}
