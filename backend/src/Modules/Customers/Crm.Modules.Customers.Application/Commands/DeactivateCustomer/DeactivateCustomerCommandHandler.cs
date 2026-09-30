using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Customers.Contracts.IntegrationEvents;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Commands.DeactivateCustomer;

public sealed class DeactivateCustomerCommandHandler(
    IRepository<Customer, CustomerId> repository,
    ICustomersUnitOfWork unitOfWork,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<DeactivateCustomerCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(DeactivateCustomerCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(request.Id), cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerDto>(CustomersErrors.CustomerNotFound(request.Id));
        }

        repository.SetOriginalRowVersion(customer, request.RowVersion);
        customer.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        eventBus.Publish(new CustomerUpdatedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, customer.Id.Value, customer.DisplayName.Arabic, customer.DisplayName.English,
            customer.Type.ToString(), customer.Status.ToString()));

        return Result.Success(CustomerDto.FromDomain(customer));
    }
}
