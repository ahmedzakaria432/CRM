using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Commands.UpdateContact;

public sealed class UpdateContactCommandHandler(IRepository<Customer, CustomerId> repository, ICustomersUnitOfWork unitOfWork)
    : IRequestHandler<UpdateContactCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(UpdateContactCommand request, CancellationToken cancellationToken)
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

        (EmailAddress? email, PhoneNumber? phone) = ContactChannels.Parse(request.Email, request.Phone);
        customer.UpdateContact(request.ContactId, request.Name, request.JobTitle, email, phone, request.HasPortalAccess);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CustomerDto.FromDomain(customer));
    }
}
