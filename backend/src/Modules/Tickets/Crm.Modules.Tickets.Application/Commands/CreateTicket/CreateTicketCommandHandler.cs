using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.CreateTicket;

public sealed class CreateTicketCommandHandler(
    IRepository<Ticket, TicketId> tickets,
    IRepository<Category, CategoryId> categories,
    ITicketsUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICustomersModuleApi customers,
    ITicketNumberGenerator numbers,
    TicketDtoAssembler assembler,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<CreateTicketCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        ScopeWriteTarget? target = currentUser.Scope.ResolveWriteTarget(request.DepartmentId);
        if (target is null)
        {
            return Result.Failure<TicketDto>(request.DepartmentId is not null
                ? TicketsErrors.DepartmentOutOfScope()
                : TicketsErrors.DepartmentRequired());
        }

        Result<CustomerSummary> customer = await customers.GetCustomerSummaryAsync(request.CustomerId, cancellationToken);
        if (customer.IsFailure)
        {
            return Result.Failure<TicketDto>(customer.Error);
        }

        if (!customer.Value.IsActive)
        {
            return Result.Failure<TicketDto>(TicketsErrors.CustomerInactive());
        }

        if (request.ContactId is { } contactId && !customer.Value.Contacts.Any(c => c.Id == contactId))
        {
            return Result.Failure<TicketDto>(TicketsErrors.ContactNotOfCustomer(contactId));
        }

        Category? category = null;
        if (request.CategoryId is { } categoryId)
        {
            category = await categories.GetByIdAsync(new CategoryId(categoryId), cancellationToken);
            if (category is null)
            {
                return Result.Failure<TicketDto>(TicketsErrors.CategoryNotFound(categoryId));
            }
        }

        TicketPriority priority = request.Priority is null
            ? TicketPriority.Normal
            : Enum.Parse<TicketPriority>(request.Priority, ignoreCase: true);

        TicketNumber number = await numbers.NextAsync(cancellationToken);
        Guid actor = CurrentActor.Id(currentUser);
        Ticket ticket = Ticket.Create(
            number, request.Subject, request.Description, request.CustomerId, request.ContactId, TicketChannel.Agent, category,
            priority, target.TenantId, target.BranchId, target.DepartmentId, actor, dateTime.UtcNow);

        tickets.Add(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Nested module reads (the assembler) happen BEFORE Publish; Publish is the last statement.
        TicketDto dto = await assembler.ToDtoAsync(ticket, cancellationToken);

        eventBus.Publish(new TicketCreatedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, ticket.Id.Value, ticket.Number.Value, ticket.CustomerId, ticket.ContactId,
            ticket.Channel.ToString(), ticket.CategoryId?.Value, ticket.Priority.ToString(), ticket.Status.ToString(),
            ticket.AssignedAgentId, actor, ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value));

        return Result.Success(dto);
    }
}
