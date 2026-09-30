using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Commands.SetPrimaryContact;

public sealed class SetPrimaryContactCommandHandler(IRepository<Customer, CustomerId> repository, ICustomersUnitOfWork unitOfWork)
    : IRequestHandler<SetPrimaryContactCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(SetPrimaryContactCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(request.CustomerId), cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerDto>(CustomersErrors.CustomerNotFound(request.CustomerId));
        }

        if (!customer.HasContact(request.ContactId))
        {
            return Result.Failure<CustomerDto>(CustomersErrors.ContactNotFound(request.ContactId));
        }

        repository.SetOriginalRowVersion(customer, request.RowVersion);
        customer.SetPrimaryContact(request.ContactId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CustomerDto.FromDomain(customer));
    }
}
