using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Customers.Application.Commands.UpdateCustomer;

/// <summary>RowVersion is the client's last-seen value (base64 over the wire); a stale one is 409.</summary>
[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record UpdateCustomerCommand(
    Guid Id, string? DisplayNameAr, string? DisplayNameEn, string Type, string? LegalName, string PreferredLanguage, byte[] RowVersion)
    : ICommand<CustomerDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CustomersCacheKeys.Customer(Id)];
}
