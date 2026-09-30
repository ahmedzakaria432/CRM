using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Customers.Application.Commands.PinNote;

/// <summary>Pin (IsPinned true) or unpin (false). Idempotent.</summary>
[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record PinNoteCommand(Guid CustomerId, Guid NoteId, bool IsPinned) : ICommand<CustomerDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CustomersCacheKeys.Customer(CustomerId)];
}
