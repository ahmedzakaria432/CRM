using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Paging;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.SearchCustomers;

[RequirePermission(CrmPermissions.CustomersRead)]
public sealed record SearchCustomersQuery(
    string? Search, string? Status, string? Type, Guid? DepartmentId, Guid? BranchId, string? Sort, bool Descending = true,
    int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<CustomerListItemDto>>>;
