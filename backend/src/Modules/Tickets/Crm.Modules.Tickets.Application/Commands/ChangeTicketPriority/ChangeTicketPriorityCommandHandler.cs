using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.ChangeTicketPriority;

public sealed class ChangeTicketPriorityCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TicketDtoAssembler assembler,
    ILocalizationService localization,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<ChangeTicketPriorityCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(ChangeTicketPriorityCommand request, CancellationToken cancellationToken)
    {
        Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        tickets.SetOriginalRowVersion(ticket, request.RowVersion);

        if (TicketRefusals.NotAllowed(ticket, TicketAction.ChangePriority, localization) is { } refusal)
        {
            return Result.Failure<TicketDto>(refusal);
        }

        Guid actor = CurrentActor.Id(currentUser);
        TicketPriority from = ticket.Priority;
        ticket.ChangePriority(Enum.Parse<TicketPriority>(request.Priority, ignoreCase: true), actor, dateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);

        eventBus.Publish(new TicketPriorityChangedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, ticket.Id.Value, ticket.Number.Value, from.ToString(), ticket.Priority.ToString(), actor,
            ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));

        return Result.Success(dto);
    }
}
