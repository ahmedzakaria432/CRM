using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.ResetUserPassword;

[RequirePermission(CrmPermissions.AdminUsers)]
public sealed record ResetUserPasswordCommand(Guid Id, string NewPassword, byte[] RowVersion) : ICommand<UserDto>;
