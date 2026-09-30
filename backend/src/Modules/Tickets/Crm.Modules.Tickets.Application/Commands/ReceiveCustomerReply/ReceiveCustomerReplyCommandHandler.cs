using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.ReceiveCustomerReply;

public sealed class ReceiveCustomerReplyCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TicketDtoAssembler assembler,
    ILocalizationService localization,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<ReceiveCustomerReplyCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(ReceiveCustomerReplyCommand request, CancellationToken cancellationToken)
    {
        Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        if (TicketRefusals.NotAllowed(ticket, TicketAction.Reply, localization) is { } refusal)
        {
            return Result.Failure<TicketDto>(refusal);
        }

        Guid actor = CurrentActor.Id(currentUser);
        TicketStatus from = ticket.Status;
        DateTimeOffset now = dateTime.UtcNow;
        TicketMessage message = ticket.ReceiveCustomerReply(request.Body, ticket.ContactId, actor, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken); // nested read BEFORE Publish

        eventBus.Publish(new TicketMessageAddedIntegrationEvent(
            Guid.NewGuid(), now, ticket.Id.Value, ticket.Number.Value, message.Id, message.Direction.ToString(), message.AuthorId,
            ticket.CustomerId, ticket.ContactId, false, actor,
            ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));

        if (ticket.Status != from)
        {
            eventBus.Publish(new TicketStatusChangedIntegrationEvent(
                Guid.NewGuid(), now, ticket.Id.Value, ticket.Number.Value, from.ToString(), ticket.Status.ToString(), actor,
                ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));
        }

        return Result.Success(dto);
    }
}
