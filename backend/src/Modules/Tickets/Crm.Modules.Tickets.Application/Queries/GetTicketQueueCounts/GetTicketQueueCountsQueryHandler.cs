using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetTicketQueueCounts;

public sealed class GetTicketQueueCountsQueryHandler(IRepository<Ticket, TicketId> tickets, ICurrentUserService currentUser)
    : IRequestHandler<GetTicketQueueCountsQuery, Result<TicketQueueCountsDto>>
{
    public async Task<Result<TicketQueueCountsDto>> Handle(GetTicketQueueCountsQuery request, CancellationToken cancellationToken)
    {
        Guid me = CurrentActor.Id(currentUser);

        int mine = await CountAsync(TicketQueueKind.Mine, me, cancellationToken);
        int unassigned = await CountAsync(TicketQueueKind.Unassigned, me, cancellationToken);
        int all = await CountAsync(TicketQueueKind.All, me, cancellationToken);

        return Result.Success(new TicketQueueCountsDto(mine, unassigned, all));
    }

    private Task<int> CountAsync(TicketQueueKind queue, Guid me, CancellationToken cancellationToken) =>
        tickets.CountAsync(
            new TicketSearchSpecification(queue, me, null, [], [], null, null, null, null, null, TicketSort.CreatedAt, true, null, null),
            cancellationToken);
}
