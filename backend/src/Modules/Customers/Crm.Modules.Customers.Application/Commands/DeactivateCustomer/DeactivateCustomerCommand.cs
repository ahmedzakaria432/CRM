using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Customers.Application.Commands.DeactivateCustomer;

[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record DeactivateCustomerCommand(Guid Id, byte[] RowVersion) : ICommand<CustomerDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CustomersCacheKeys.Customer(Id)];
}
