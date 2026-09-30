using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Commands.AddContact;

public sealed class AddContactCommandHandler(IRepository<Customer, CustomerId> repository, ICustomersUnitOfWork unitOfWork)
    : IRequestHandler<AddContactCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(AddContactCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(request.CustomerId), cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerDto>(CustomersErrors.CustomerNotFound(request.CustomerId));
        }

        repository.SetOriginalRowVersion(customer, request.RowVersion);

        (EmailAddress? email, PhoneNumber? phone) = ContactChannels.Parse(request.Email, request.Phone);
        customer.AddContact(request.Name, request.JobTitle, email, phone, request.IsPrimary, request.HasPortalAccess);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CustomerDto.FromDomain(customer));
    }
}
