using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetPortalTicket;

public sealed class GetPortalTicketQueryHandler(
    IPortalTicketReadModel readModel, ICurrentUserService currentUser, PortalTicketMapper mapper)
    : IRequestHandler<GetPortalTicketQuery, Result<PortalTicketDto>>
{
    public async Task<Result<PortalTicketDto>> Handle(GetPortalTicketQuery request, CancellationToken cancellationToken)
    {
        if (PortalCaller.Grant(currentUser) is not { } grant)
        {
            return Result.Failure<PortalTicketDto>(TicketsErrors.PortalOnly());
        }

        // The read model, never GetByIdAsync: a miss on an existing row would become 403 and confirm the ticket exists (D11).
        IReadOnlyList<PortalTicketRow> rows = await readModel.ListAsync(
            new PortalTicketsSpecification(grant, new TicketId(request.Id), [], null, null, null, tracked: false), cancellationToken);

        return rows.Count == 0
            ? Result.Failure<PortalTicketDto>(TicketsErrors.TicketNotFound(request.Id))
            : Result.Success(await mapper.ToDtoAsync(rows[0], cancellationToken));
    }
}
