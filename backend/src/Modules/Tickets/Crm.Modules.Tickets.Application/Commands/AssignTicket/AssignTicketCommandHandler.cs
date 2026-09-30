using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.AssignTicket;

public sealed class AssignTicketCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IOrganisationModuleApi organisation,
    IIdentityModuleApi identity,
    TicketDtoAssembler assembler,
    ILocalizationService localization,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<AssignTicketCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(AssignTicketCommand request, CancellationToken cancellationToken)
    {
        Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        tickets.SetOriginalRowVersion(ticket, request.RowVersion);

        if (TicketRefusals.NotAllowed(ticket, TicketAction.Assign, localization) is { } refusal)
        {
            return Result.Failure<TicketDto>(refusal);
        }

        Result<IReadOnlyList<StaffUserSummary>> users = await identity.GetStaffUsersAsync([request.AgentId], cancellationToken);
        if (users.IsSuccess && users.Value.Any(u => u.Id == request.AgentId && !u.IsActive))
        {
            return Result.Failure<TicketDto>(TicketsErrors.AssigneeInactive(request.AgentId));
        }

        Result<bool> member = await organisation.IsDepartmentMemberAsync(request.AgentId, ticket.DepartmentId.Value, cancellationToken);
        if (member.IsFailure || !member.Value)
        {
            return Result.Failure<TicketDto>(TicketsErrors.AssigneeNotInDepartment(request.AgentId));
        }

        Guid actor = CurrentActor.Id(currentUser);
        Guid? previous = ticket.AssignedAgentId;
        ticket.AssignTo(request.AgentId, actor, dateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);

        eventBus.Publish(new TicketAssignedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, ticket.Id.Value, ticket.Number.Value, previous, request.AgentId, actor,
            ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));

        return Result.Success(dto);
    }
}
