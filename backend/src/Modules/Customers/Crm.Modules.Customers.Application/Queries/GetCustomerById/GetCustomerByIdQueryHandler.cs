using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.GetCustomerById;

public sealed class GetCustomerByIdQueryHandler(IRepository<Customer, CustomerId> repository)
    : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.FirstOrDefaultAsync(
            new CustomerByIdSpecification(new CustomerId(request.Id)), cancellationToken);

        return customer is null
            ? Result.Failure<CustomerDto>(CustomersErrors.CustomerNotFound(request.Id))
            : Result.Success(CustomerDto.FromDomain(customer));
    }
}
