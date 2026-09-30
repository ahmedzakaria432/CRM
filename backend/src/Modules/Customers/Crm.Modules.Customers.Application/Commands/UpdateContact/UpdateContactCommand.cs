using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Customers.Application.Commands.UpdateContact;

[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record UpdateContactCommand(
    Guid CustomerId, Guid ContactId, string Name, string? JobTitle, string? Email, string? Phone, bool HasPortalAccess, byte[] RowVersion)
    : ICommand<CustomerDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CustomersCacheKeys.Customer(CustomerId)];
}
