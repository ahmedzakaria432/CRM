using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.RefreshSession;

/// <summary>The refresh token is the credential. A rejected token is a committed fact (reuse revokes the family),
/// so the handler returns a successful Result with <see cref="SignInOutcome.Succeeded"/> = false.</summary>
[AllowAnonymousRequest]
public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<SignInOutcome>;
