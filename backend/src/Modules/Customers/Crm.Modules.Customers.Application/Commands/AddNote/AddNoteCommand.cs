using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Customers.Application.Commands.AddNote;

/// <summary>Notes carry no cross-note invariant, so this takes no RowVersion: two agents adding notes never conflict.</summary>
[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record AddNoteCommand(Guid CustomerId, string Body) : ICommand<CustomerDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CustomersCacheKeys.Customer(CustomerId)];
}
