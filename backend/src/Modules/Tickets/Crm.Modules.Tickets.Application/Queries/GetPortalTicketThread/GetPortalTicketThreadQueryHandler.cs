using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetPortalTicketThread;

public sealed class GetPortalTicketThreadQueryHandler(
    IPortalTicketReadModel portalTickets, ITicketThreadReadModel threads, ICurrentUserService currentUser)
    : IRequestHandler<GetPortalTicketThreadQuery, Result<PortalThreadPageDto>>
{
    public async Task<Result<PortalThreadPageDto>> Handle(GetPortalTicketThreadQuery request, CancellationToken cancellationToken)
    {
        if (PortalCaller.Grant(currentUser) is not { } grant)
        {
            return Result.Failure<PortalThreadPageDto>(TicketsErrors.PortalOnly());
        }

        var id = new TicketId(request.Id);

        // Ownership first: this is what enforces the contact rule of a non-admin (ListPublicAsync only sees the scope filter).
        IReadOnlyList<PortalTicketRow> owned = await portalTickets.ListAsync(
            new PortalTicketsSpecification(grant, id, [], null, null, null, tracked: false), cancellationToken);

        if (owned.Count == 0)
        {
            return Result.Failure<PortalThreadPageDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        // The ONLY thread method named here: messages, never notes or activities (D12).
        IReadOnlyList<TicketThreadRow>? rows = await threads.ListPublicAsync(id, request.After ?? 0, request.Limit + 1, cancellationToken);

        if (rows is null)
        {
            return Result.Failure<PortalThreadPageDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        List<TicketThreadRow> page = [.. rows.Take(request.Limit)];
        int? next = rows.Count > request.Limit ? page[^1].Sequence : null;

        return Result.Success(new PortalThreadPageDto(
            [.. page.Select(r => new PortalThreadMessageDto(
                r.Sequence,
                r.Id,
                r.Direction == TicketMessageDirection.Inbound ? "Customer" : "Support",
                r.Direction == TicketMessageDirection.Inbound && r.AuthorId == grant.ContactId,
                r.Body ?? string.Empty,
                r.OccurredAtUtc))],
            next));
    }
}
