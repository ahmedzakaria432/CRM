using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.RefreshPortalSession;

/// <summary>The refresh token is the credential. A rejected token is a committed fact (reuse revokes the session),
/// so the handler returns a successful Result with <see cref="SignInOutcome.Succeeded"/> = false (Story 47).</summary>
[AllowAnonymousRequest]
public sealed record RefreshPortalSessionCommand(string RefreshToken) : ICommand<SignInOutcome>;
