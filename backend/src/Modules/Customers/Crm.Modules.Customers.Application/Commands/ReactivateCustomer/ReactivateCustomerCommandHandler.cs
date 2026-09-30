using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Customers.Contracts.IntegrationEvents;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Commands.ReactivateCustomer;

public sealed class ReactivateCustomerCommandHandler(
    IRepository<Customer, CustomerId> repository,
    ICustomersUnitOfWork unitOfWork,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<ReactivateCustomerCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(ReactivateCustomerCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(request.Id), cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerDto>(CustomersErrors.CustomerNotFound(request.Id));
        }

        repository.SetOriginalRowVersion(customer, request.RowVersion);
        customer.Reactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        eventBus.Publish(new CustomerUpdatedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, customer.Id.Value, customer.DisplayName.Arabic, customer.DisplayName.English,
            customer.Type.ToString(), customer.Status.ToString()));

        return Result.Success(CustomerDto.FromDomain(customer));
    }
}
