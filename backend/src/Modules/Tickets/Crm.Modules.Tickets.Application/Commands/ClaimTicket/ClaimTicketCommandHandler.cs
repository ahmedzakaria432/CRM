using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Tickets.Application.Commands.ClaimTicket;

public sealed partial class ClaimTicketCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IIdentityModuleApi identity,
    TicketDtoAssembler assembler,
    ILocalizationService localization,
    IDateTime dateTime,
    IEventBus eventBus,
    ILogger<ClaimTicketCommandHandler> logger)
    : IRequestHandler<ClaimTicketCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(ClaimTicketCommand request, CancellationToken cancellationToken)
    {
        Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        if (ticket.AssignedAgentId is { } assignee)
        {
            return Result.Failure<TicketDto>(TicketRefusals.AlreadyClaimed(await NameOfAsync(assignee, cancellationToken), localization));
        }

        if (TicketRefusals.NotAllowed(ticket, TicketAction.Claim, localization) is { } refusal)
        {
            return Result.Failure<TicketDto>(refusal);
        }

        Guid me = CurrentActor.Id(currentUser);
        ticket.Claim(me, dateTime.UtcNow);

        try
        {
            // UPDATE ... WHERE [RowVersion] = <the loaded value>: a concurrent claim that committed first makes this 0 rows.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            Ticket? current = await tickets.FirstOrDefaultAsync(new TicketByIdSpecification(ticket.Id), cancellationToken);
            if (current?.AssignedAgentId is not { } winner)
            {
                throw; // some other change won - TransactionBehavior answers the generic 409
            }

            LogClaimLostRace(ticket.Number.Value, winner);
            return Result.Failure<TicketDto>(TicketRefusals.AlreadyClaimed(await NameOfAsync(winner, cancellationToken), localization));
        }

        TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);

        eventBus.Publish(new TicketAssignedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, ticket.Id.Value, ticket.Number.Value, null, me, me, ticket.TenantId.Value,
            ticket.BranchId.Value, ticket.DepartmentId.Value));

        return Result.Success(dto);
    }

    private async Task<string> NameOfAsync(Guid id, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<StaffUserSummary>> users = await identity.GetStaffUsersAsync([id], cancellationToken);
        string? name = users.IsSuccess ? users.Value.FirstOrDefault(u => u.Id == id)?.DisplayName : null;
        return name ?? id.ToString();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Claim of ticket {TicketNumber} lost the race to {WinnerId}")]
    private partial void LogClaimLostRace(string ticketNumber, Guid winnerId);
}
