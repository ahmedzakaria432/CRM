using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.RegisterPortalAccount;

/// <summary>Always a success unless the input is malformed: the answer never says whether the email belongs to a
/// customer (Story 47, D5).</summary>
[AllowAnonymousRequest]
public sealed record RegisterPortalAccountCommand(string Email, string Password, string? PreferredLocale) : ICommand;
