using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Customers.Application.Commands.SetPrimaryContact;

[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record SetPrimaryContactCommand(Guid CustomerId, Guid ContactId, byte[] RowVersion)
    : ICommand<CustomerDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CustomersCacheKeys.Customer(CustomerId)];
}
