using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.RequestTicketClosure;

public sealed class RequestTicketClosureCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    PortalTicketMapper mapper,
    ILocalizationService localization,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<RequestTicketClosureCommand, Result<PortalTicketDto>>
{
    public async Task<Result<PortalTicketDto>> Handle(RequestTicketClosureCommand request, CancellationToken cancellationToken)
    {
        if (PortalCaller.Grant(currentUser) is not { } grant)
        {
            return Result.Failure<PortalTicketDto>(TicketsErrors.PortalOnly());
        }

        IReadOnlyList<Ticket> found = await tickets.ListAsync(
            new PortalTicketsSpecification(grant, new TicketId(request.Id), [], null, null, null, tracked: true), cancellationToken);
        Ticket? ticket = found.Count == 0 ? null : found[0];

        if (ticket is null)
        {
            return Result.Failure<PortalTicketDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        if (TicketRefusals.CustomerNotAllowed(ticket, CustomerTicketAction.RequestClosure, localization) is { } refusal)
        {
            return Result.Failure<PortalTicketDto>(refusal);
        }

        TicketStatus from = ticket.Status;
        DateTimeOffset now = dateTime.UtcNow;
        ticket.RequestClosure(grant.ContactId, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        PortalTicketDto dto = await mapper.ToDtoAsync(ticket, cancellationToken);

        eventBus.Publish(new TicketStatusChangedIntegrationEvent(
            Guid.NewGuid(), now, ticket.Id.Value, ticket.Number.Value, from.ToString(), ticket.Status.ToString(), grant.ContactId,
            ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));

        return Result.Success(dto);
    }
}
