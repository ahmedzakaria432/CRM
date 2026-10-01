using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.RevertBranding;

[RequirePermission(CrmPermissions.AdminBranding)]
public sealed record RevertBrandingCommand(byte[] RowVersion) : ICommand<BrandingSettingsDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [OrganisationCacheKeys.Brandings];
}
