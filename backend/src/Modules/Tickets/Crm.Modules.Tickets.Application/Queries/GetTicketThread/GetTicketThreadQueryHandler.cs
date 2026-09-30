using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetTicketThread;

public sealed class GetTicketThreadQueryHandler(
    ITicketThreadReadModel threads, IRepository<Ticket, TicketId> tickets, TicketDtoAssembler assembler)
    : IRequestHandler<GetTicketThreadQuery, Result<TicketThreadPageDto>>
{
    public async Task<Result<TicketThreadPageDto>> Handle(GetTicketThreadQuery request, CancellationToken cancellationToken)
    {
        List<TicketThreadVisibility> visibilities = request.Visibility.Count == 0
            ? [TicketThreadVisibility.Public, TicketThreadVisibility.Internal, TicketThreadVisibility.System]
            : [.. request.Visibility.Select(v => Enum.Parse<TicketThreadVisibility>(v, ignoreCase: true)).Distinct()];
        var id = new TicketId(request.TicketId);
        int after = request.After ?? 0;
        int take = request.Limit + 1; // one extra row tells whether a next page exists

        IReadOnlyList<TicketThreadRow>? rows = visibilities is [TicketThreadVisibility.Public]
            ? await threads.ListPublicAsync(id, after, take, cancellationToken)
            : await threads.ListAsync(id, visibilities, after, take, cancellationToken);

        if (rows is null)
        {
            // Not visible: an existing out-of-scope ticket throws here (-> 403 Organisation.OutOfScope, Story 06);
            // unknown or deleted -> 404.
            _ = await tickets.GetByIdAsync(id, cancellationToken);
            return Result.Failure<TicketThreadPageDto>(TicketsErrors.TicketNotFound(request.TicketId));
        }

        return Result.Success(await assembler.ToThreadPageAsync(rows, request.Limit, cancellationToken));
    }
}
