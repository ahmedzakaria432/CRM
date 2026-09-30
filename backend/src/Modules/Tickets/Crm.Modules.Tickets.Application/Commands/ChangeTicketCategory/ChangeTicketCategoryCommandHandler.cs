using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.ChangeTicketCategory;

public sealed class ChangeTicketCategoryCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    IRepository<Category, CategoryId> categories,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TicketDtoAssembler assembler,
    ILocalizationService localization,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<ChangeTicketCategoryCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(ChangeTicketCategoryCommand request, CancellationToken cancellationToken)
    {
        Ticket? ticket = await tickets.GetByIdAsync(new TicketId(request.Id), cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketDto>(TicketsErrors.TicketNotFound(request.Id));
        }

        tickets.SetOriginalRowVersion(ticket, request.RowVersion);

        if (TicketRefusals.NotAllowed(ticket, TicketAction.ChangeCategory, localization) is { } refusal)
        {
            return Result.Failure<TicketDto>(refusal);
        }

        Category? category = await categories.GetByIdAsync(new CategoryId(request.CategoryId), cancellationToken);
        if (category is null)
        {
            return Result.Failure<TicketDto>(TicketsErrors.CategoryNotFound(request.CategoryId));
        }

        Guid actor = CurrentActor.Id(currentUser);
        Guid? from = ticket.CategoryId?.Value;
        ticket.ChangeCategory(category, actor, dateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);

        eventBus.Publish(new TicketCategoryChangedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, ticket.Id.Value, ticket.Number.Value, from, category.Id.Value, actor,
            ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));

        return Result.Success(dto);
    }
}
