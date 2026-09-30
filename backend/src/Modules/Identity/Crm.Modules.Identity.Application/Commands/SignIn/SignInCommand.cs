using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.SignIn;

/// <summary>Sign-in has no user yet. A wrong password is a committed fact (the failed-attempt counter), so the
/// handler returns a successful Result carrying <see cref="SignInOutcome.Succeeded"/> = false.</summary>
[AllowAnonymousRequest]
public sealed record SignInCommand(string Email, string Password) : ICommand<SignInOutcome>;
