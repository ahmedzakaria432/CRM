using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetBrandingSettings;

/// <summary>The admin view with the live row version - deliberately not cached.</summary>
[RequirePermission(CrmPermissions.AdminBranding)]
public sealed record GetBrandingSettingsQuery : IRequest<Result<BrandingSettingsDto>>;
