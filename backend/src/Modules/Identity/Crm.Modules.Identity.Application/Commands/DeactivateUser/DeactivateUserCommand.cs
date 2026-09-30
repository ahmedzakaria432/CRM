using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.DeactivateUser;

[RequirePermission(CrmPermissions.AdminUsers)]
public sealed record DeactivateUserCommand(Guid Id, byte[] RowVersion) : ICommand<UserDto>;
