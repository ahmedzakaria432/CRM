using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.GetCustomerById;

/// <summary>Cached by CachingBehavior (culture-neutral: both name languages); every customer command invalidates the same tag.</summary>
[RequirePermission(CrmPermissions.CustomersRead)]
public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<Result<CustomerDto>>, ICacheableQuery
{
    public string CacheKey => CustomersCacheKeys.Customer(Id);

    public IReadOnlyCollection<string> CacheTags => [CustomersCacheKeys.Customer(Id)];
}
