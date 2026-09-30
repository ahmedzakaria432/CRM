using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.UpdatePortalProfile;

[RequirePermission(CrmPermissions.PortalAccount)]
public sealed record UpdatePortalProfileCommand(string DisplayName, string PreferredLocale) : ICommand<PortalMeDto>;
