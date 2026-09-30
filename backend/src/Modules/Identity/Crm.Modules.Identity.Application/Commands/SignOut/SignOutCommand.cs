using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.SignOut;

/// <summary>Idempotent and no oracle: an unknown or missing token is still a success.</summary>
[AllowAnonymousRequest]
public sealed record SignOutCommand(string? RefreshToken) : ICommand;
