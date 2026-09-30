using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.ChangePassword;

[RequireAuthenticatedUser]
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
