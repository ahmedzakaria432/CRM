using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.ChangeMembershipRole;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record ChangeMembershipRoleCommand(Guid Id, string Role, byte[] RowVersion) : ICommand<MembershipDto>;
