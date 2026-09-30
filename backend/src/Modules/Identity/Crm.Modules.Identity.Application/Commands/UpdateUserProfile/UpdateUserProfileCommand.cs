using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.UpdateUserProfile;

[RequirePermission(CrmPermissions.AdminUsers)]
public sealed record UpdateUserProfileCommand(Guid Id, string DisplayName, string PreferredLocale, byte[] RowVersion)
    : ICommand<UserDto>;
