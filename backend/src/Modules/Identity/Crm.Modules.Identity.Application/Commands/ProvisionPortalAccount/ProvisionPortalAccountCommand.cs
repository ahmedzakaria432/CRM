using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Identity.Application.Commands.ProvisionPortalAccount;

/// <summary>An agent sets (or resets) the password of a portal-enabled contact; the account is created on first use
/// (Story 47, D19).</summary>
[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record ProvisionPortalAccountCommand(Guid CustomerId, Guid ContactId, string Password) : ICommand<PortalAccountDto>;
