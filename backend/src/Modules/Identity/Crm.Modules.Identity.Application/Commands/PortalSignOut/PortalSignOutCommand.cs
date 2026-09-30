using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.PortalSignOut;

/// <summary>Idempotent and no oracle: an unknown or missing token is still a success (Story 47).</summary>
[AllowAnonymousRequest]
public sealed record PortalSignOutCommand(string? RefreshToken) : ICommand;
