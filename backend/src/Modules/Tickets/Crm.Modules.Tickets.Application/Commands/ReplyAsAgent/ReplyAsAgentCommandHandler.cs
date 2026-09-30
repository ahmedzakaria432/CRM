using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.ReplyAsAgent;

public sealed class ReplyAsAgentCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TicketDtoAssembler assembler,
    ILocalizationService localization,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<ReplyAsAgentCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(ReplyAsAgentCommand request, CancellationToken cancellationToken)
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
        bool firstResponse = ticket.FirstRespondedAtUtc is null;
        DateTimeOffset now = dateTime.UtcNow;
        TicketMessage message = ticket.ReplyAsAgent(request.Body, actor, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken); // nested read BEFORE Publish

        eventBus.Publish(new TicketMessageAddedIntegrationEvent(
            Guid.NewGuid(), now, ticket.Id.Value, ticket.Number.Value, message.Id, message.Direction.ToString(), message.AuthorId,
            ticket.CustomerId, ticket.ContactId, firstResponse && ticket.FirstRespondedAtUtc is not null, actor,
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
