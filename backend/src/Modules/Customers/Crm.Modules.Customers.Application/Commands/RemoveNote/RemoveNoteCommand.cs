using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Customers.Application.Commands.RemoveNote;

[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record RemoveNoteCommand(Guid CustomerId, Guid NoteId) : ICommand<CustomerDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CustomersCacheKeys.Customer(CustomerId)];
}
