using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.GetCustomerNames;

public sealed class GetCustomerNamesQueryHandler(IRepository<Customer, CustomerId> repository)
    : IRequestHandler<GetCustomerNamesQuery, Result<IReadOnlyList<CustomerNameSummary>>>
{
    public async Task<Result<IReadOnlyList<CustomerNameSummary>>> Handle(GetCustomerNamesQuery request, CancellationToken cancellationToken)
    {
        if (request.CustomerIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<CustomerNameSummary>>([]);
        }

        IReadOnlyList<Customer> customers = await repository.ListAsync(
            new CustomersByIdsSpecification([.. request.CustomerIds.Distinct().Select(id => new CustomerId(id))]), cancellationToken);

        return Result.Success<IReadOnlyList<CustomerNameSummary>>(
            [.. customers.Select(c => new CustomerNameSummary(c.Id.Value, c.Number.Value, c.DisplayName.Arabic, c.DisplayName.English))]);
    }
}
