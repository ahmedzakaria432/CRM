using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Customers.Application.Commands.AddContact;

[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record AddContactCommand(
    Guid CustomerId, string Name, string? JobTitle, string? Email, string? Phone, bool IsPrimary, bool HasPortalAccess, byte[] RowVersion)
    : ICommand<CustomerDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CustomersCacheKeys.Customer(CustomerId)];
}
