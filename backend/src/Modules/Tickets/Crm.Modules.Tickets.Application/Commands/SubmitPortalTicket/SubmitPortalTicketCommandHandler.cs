using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.SubmitPortalTicket;

public sealed class SubmitPortalTicketCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    IRepository<Category, CategoryId> categories,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ITicketNumberGenerator numbers,
    PortalTicketMapper mapper,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<SubmitPortalTicketCommand, Result<PortalTicketDto>>
{
    public async Task<Result<PortalTicketDto>> Handle(SubmitPortalTicketCommand request, CancellationToken cancellationToken)
    {
        if (PortalCaller.Grant(currentUser) is not { } grant)
        {
            return Result.Failure<PortalTicketDto>(TicketsErrors.PortalOnly());
        }

        Category? category = null;
        if (request.CategoryId is { } categoryId)
        {
            // From the customer-visible list of the home department, never GetByIdAsync: an id of another tenant would be a 403.
            IReadOnlyList<Category> visible = await categories.ListAsync(
                new CategoriesSpecification([grant.HomeDepartmentId], activeOnly: true, customerVisibleOnly: true), cancellationToken);
            category = visible.FirstOrDefault(c => c.Id.Value == categoryId);

            if (category is null)
            {
                return Result.Failure<PortalTicketDto>(TicketsErrors.CategoryNotFound(categoryId));
            }
        }

        TicketNumber number = await numbers.NextAsync(cancellationToken);
        DateTimeOffset now = dateTime.UtcNow;
        Ticket ticket = Ticket.Create(
            number, request.Subject, request.Description, grant.CustomerId, grant.ContactId, TicketChannel.Portal, category,
            TicketPriority.Normal, currentUser.Scope.Tenant, grant.HomeBranchId, grant.HomeDepartmentId, grant.ContactId, now);

        tickets.Add(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Nested reads (the mapper) happen BEFORE Publish; Publish is the last statement.
        PortalTicketDto dto = await mapper.ToDtoAsync(ticket, cancellationToken);

        eventBus.Publish(new TicketCreatedIntegrationEvent(
            Guid.NewGuid(), now, ticket.Id.Value, ticket.Number.Value, ticket.CustomerId, ticket.ContactId,
            ticket.Channel.ToString(), ticket.CategoryId?.Value, ticket.Priority.ToString(), ticket.Status.ToString(),
            ticket.AssignedAgentId, grant.ContactId, ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));

        return Result.Success(dto);
    }
}
