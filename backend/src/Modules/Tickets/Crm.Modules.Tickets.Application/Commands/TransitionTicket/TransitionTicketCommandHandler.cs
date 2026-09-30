using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.TransitionTicket;

public sealed class TransitionTicketCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TicketDtoAssembler assembler,
    ILocalizationService localization,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<TransitionTicketCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(TransitionTicketCommand request, CancellationToken cancellationToken)
    {
        Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        tickets.SetOriginalRowVersion(ticket, request.RowVersion);

        TicketAction action = Enum.Parse<TicketAction>(request.Action, ignoreCase: true);
        if (TicketRefusals.NotAllowed(ticket, action, localization) is { } refusal)
        {
            return Result.Failure<TicketDto>(refusal);
        }

        Guid actor = CurrentActor.Id(currentUser);
        TicketStatus from = ticket.Status;
        DateTimeOffset now = dateTime.UtcNow;

        switch (action)
        {
            case TicketAction.Open:
                ticket.Open(actor, now);
                break;
            case TicketAction.AwaitCustomer:
                ticket.AwaitCustomer(actor, now);
                break;
            case TicketAction.PutOnHold:
                ticket.PutOnHold(actor, now);
                break;
            case TicketAction.Resolve:
                ticket.Resolve(actor, now);
                break;
            case TicketAction.Close:
                ticket.Close(actor, now);
                break;
            case TicketAction.Reopen:
                ticket.Reopen(actor, now);
                break;
            case TicketAction.Cancel:
                ticket.Cancel(actor, now);
                break;
            default:
                return Result.Failure<TicketDto>(TicketsErrors.TransitionNotAllowed(action.ToString()));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);

        eventBus.Publish(new TicketStatusChangedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, ticket.Id.Value, ticket.Number.Value, from.ToString(), ticket.Status.ToString(), actor,
            ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));

        return Result.Success(dto);
    }
}
