// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Sample.Application.Commands.DeleteSample;

[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record DeleteSampleCommand(Guid Id) : ICommand, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [SampleCacheKeys.Sample(Id)];
}
