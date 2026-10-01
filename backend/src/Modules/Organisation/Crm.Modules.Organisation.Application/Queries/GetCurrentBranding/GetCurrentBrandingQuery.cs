using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetCurrentBranding;

/// <summary>The branding of the signed-in staff user own tenant, for the agent and admin shell (D5). Cached per tenant.</summary>
[RequireAuthenticatedUser]
public sealed record GetCurrentBrandingQuery : IRequest<Result<PublicBrandingDto>>, ICacheableQuery
{
    public string CacheKey => OrganisationCacheKeys.CurrentBranding;

    public IReadOnlyCollection<string> CacheTags => [OrganisationCacheKeys.Brandings];
}
