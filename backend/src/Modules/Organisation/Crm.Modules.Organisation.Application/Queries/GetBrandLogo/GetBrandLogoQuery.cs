using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetBrandLogo;

/// <summary>A published logo, fetched anonymously by its random id (S1, S4). Not cacheable: bytes stay out of Redis (D12).</summary>
[AllowAnonymousRequest]
public sealed record GetBrandLogoQuery(Guid Id) : IRequest<Result<BrandLogoContentDto>>;
