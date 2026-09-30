using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.ChangePortalPassword;

[RequirePermission(CrmPermissions.PortalAccount)]
public sealed record ChangePortalPasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
