using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.PreviewBranding;

/// <summary>Tokens and contrast checks for a candidate palette. A failing palette still returns 200 with Passes = false:
/// the preview has to show bad colours.</summary>
[RequirePermission(CrmPermissions.AdminBranding)]
public sealed record PreviewBrandingQuery(string PrimaryColour, string AccentColour) : IRequest<Result<BrandingPreviewDto>>;
