using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.TransferTicket;

public sealed class TransferTicketCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IOrganisationModuleApi organisation,
    TicketDtoAssembler assembler,
    ILocalizationService localization,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<TransferTicketCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(TransferTicketCommand request, CancellationToken cancellationToken)
    {
        Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        tickets.SetOriginalRowVersion(ticket, request.RowVersion);

        if (TicketRefusals.NotAllowed(ticket, TicketAction.Transfer, localization) is { } refusal)
        {
            return Result.Failure<TicketDto>(refusal);
        }

        Result<DepartmentReference> department = await organisation.GetDepartmentAsync(request.DepartmentId, cancellationToken);
        if (department.IsFailure)
        {
            return Result.Failure<TicketDto>(department.Error);
        }

        if (!department.Value.IsActive)
        {
            return Result.Failure<TicketDto>(TicketsErrors.TargetDepartmentInactive());
        }

        Guid actor = CurrentActor.Id(currentUser);
        Guid? previousAgent = ticket.AssignedAgentId;
        Guid fromBranch = ticket.BranchId.Value;
        Guid fromDepartment = ticket.DepartmentId.Value;

        ticket.TransferTo(new DepartmentId(department.Value.Id), new BranchId(department.Value.BranchId), actor, dateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);

        eventBus.Publish(new TicketTransferredIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, ticket.Id.Value, ticket.Number.Value, fromBranch, fromDepartment,
            ticket.BranchId.Value, ticket.DepartmentId.Value, previousAgent, actor, ticket.TenantId.Value));

        return Result.Success(dto);
    }
}
