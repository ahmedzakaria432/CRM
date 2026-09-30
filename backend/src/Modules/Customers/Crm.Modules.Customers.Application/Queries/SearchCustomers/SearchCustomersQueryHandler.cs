using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Paging;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.SearchCustomers;

public sealed class SearchCustomersQueryHandler(IRepository<Customer, CustomerId> repository)
    : IRequestHandler<SearchCustomersQuery, Result<PagedResult<CustomerListItemDto>>>
{
    public async Task<Result<PagedResult<CustomerListItemDto>>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        CustomerStatus? status = request.Status is null ? null : Enum.Parse<CustomerStatus>(request.Status, ignoreCase: true);
        CustomerType? type = request.Type is null ? null : Enum.Parse<CustomerType>(request.Type, ignoreCase: true);
        CustomerSort sort = CustomerSort.CreatedAt;
        if (request.Sort is not null && !CustomerSortParser.TryParse(request.Sort, out sort))
        {
            sort = CustomerSort.CreatedAt;
        }

        IReadOnlyList<Customer> page = await repository.ListAsync(
            new CustomerSearchSpecification(
                request.Search, status, type, request.DepartmentId, request.BranchId, sort, request.Descending, request.Page, request.PageSize),
            cancellationToken);

        int total = await repository.CountAsync(
            new CustomerSearchSpecification(
                request.Search, status, type, request.DepartmentId, request.BranchId, sort, request.Descending, null, null),
            cancellationToken);

        return Result.Success(new PagedResult<CustomerListItemDto>(
            [.. page.Select(CustomerListItemDto.FromDomain)], total, request.Page, request.PageSize));
    }
}
