using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.CreateUser;

[RequirePermission(CrmPermissions.AdminUsers)]
public sealed record CreateUserCommand(string Email, string DisplayName, string PreferredLocale, string InitialPassword)
    : ICommand<UserDto>;
