using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Customers.Contracts.IntegrationEvents;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler(
    IRepository<Customer, CustomerId> repository,
    ICustomersUnitOfWork unitOfWork,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<UpdateCustomerCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(request.Id), cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerDto>(CustomersErrors.CustomerNotFound(request.Id));
        }

        repository.SetOriginalRowVersion(customer, request.RowVersion);
        customer.UpdateProfile(
            LocalizedText.Create(request.DisplayNameAr, request.DisplayNameEn),
            Enum.Parse<CustomerType>(request.Type, ignoreCase: true),
            request.LegalName,
            PreferredLanguage.Create(request.PreferredLanguage));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        eventBus.Publish(new CustomerUpdatedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, customer.Id.Value, customer.DisplayName.Arabic, customer.DisplayName.English,
            customer.Type.ToString(), customer.Status.ToString()));

        return Result.Success(CustomerDto.FromDomain(customer));
    }
}
