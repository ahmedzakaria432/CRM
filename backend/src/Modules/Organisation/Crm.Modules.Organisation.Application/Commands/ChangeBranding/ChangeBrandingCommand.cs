using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.ChangeBranding;

[RequirePermission(CrmPermissions.AdminBranding)]
public sealed record ChangeBrandingCommand(
    string DisplayNameAr,
    string DisplayNameEn,
    string PrimaryColour,
    string AccentColour,
    string ArabicFont,
    Guid? LightLogoId,
    Guid? DarkLogoId,
    byte[] RowVersion) : ICommand<BrandingSettingsDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [OrganisationCacheKeys.Brandings];
}
