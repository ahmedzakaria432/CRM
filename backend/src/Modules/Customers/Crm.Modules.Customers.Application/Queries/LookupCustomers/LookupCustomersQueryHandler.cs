using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.LookupCustomers;

public sealed class LookupCustomersQueryHandler(IRepository<Customer, CustomerId> repository)
    : IRequestHandler<LookupCustomersQuery, Result<IReadOnlyList<CustomerLookupDto>>>
{
    public async Task<Result<IReadOnlyList<CustomerLookupDto>>> Handle(LookupCustomersQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Customer> customers = await repository.ListAsync(
            new CustomerLookupSpecification(request.Search, request.Take), cancellationToken);

        return Result.Success<IReadOnlyList<CustomerLookupDto>>([.. customers.Select(CustomerLookupDto.FromDomain)]);
    }
}
