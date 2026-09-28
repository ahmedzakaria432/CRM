// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Caching;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application.Queries.GetSampleById;

/// <summary>Cached by CachingBehavior; RenameSampleCommand and DeleteSampleCommand invalidate the same tag.</summary>
public sealed record GetSampleByIdQuery(Guid Id) : IRequest<Result<SampleAggregateDto>>, ICacheableQuery
{
    public string CacheKey => SampleCacheKeys.Sample(Id);

    public IReadOnlyCollection<string> CacheTags => [SampleCacheKeys.Sample(Id)];
}
