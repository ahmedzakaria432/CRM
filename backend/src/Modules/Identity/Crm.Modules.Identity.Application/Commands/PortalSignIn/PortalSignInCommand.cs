using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.PortalSignIn;

/// <summary>Portal sign-in (Story 47). Like SignInCommand, a wrong password is a committed fact, so the handler
/// returns a successful Result carrying <see cref="SignInOutcome.Succeeded"/> = false.</summary>
[AllowAnonymousRequest]
public sealed record PortalSignInCommand(string Email, string Password) : ICommand<SignInOutcome>;
