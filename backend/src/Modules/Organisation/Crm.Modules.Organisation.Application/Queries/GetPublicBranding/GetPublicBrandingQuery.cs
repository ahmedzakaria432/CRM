using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetPublicBranding;

/// <summary>The branding of the portal sign-in pages, anonymous by design (S1, S4). Cached; ChangeBranding/RevertBranding invalidate.</summary>
[AllowAnonymousRequest]
public sealed record GetPublicBrandingQuery(string Organisation) : IRequest<Result<PublicBrandingDto>>, ICacheableQuery
{
    public string CacheKey => OrganisationCacheKeys.PublicBranding(Organisation);

    public IReadOnlyCollection<string> CacheTags => [OrganisationCacheKeys.Brandings];
}
