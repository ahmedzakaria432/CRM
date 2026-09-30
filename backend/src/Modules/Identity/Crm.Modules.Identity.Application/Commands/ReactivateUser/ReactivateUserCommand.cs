using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.ReactivateUser;

[RequirePermission(CrmPermissions.AdminUsers)]
public sealed record ReactivateUserCommand(Guid Id, byte[] RowVersion) : ICommand<UserDto>;
