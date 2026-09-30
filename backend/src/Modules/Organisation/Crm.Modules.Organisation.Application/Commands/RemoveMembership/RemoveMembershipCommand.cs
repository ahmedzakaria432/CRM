using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.RemoveMembership;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record RemoveMembershipCommand(Guid Id) : ICommand;
