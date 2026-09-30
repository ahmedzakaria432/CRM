using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.AddMembership;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record AddMembershipCommand(Guid UserId, Guid DepartmentId, string Role) : ICommand<MembershipDto>;
