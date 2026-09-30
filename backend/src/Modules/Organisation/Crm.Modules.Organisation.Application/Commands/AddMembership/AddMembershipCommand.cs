using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.AddMembership;

public sealed record AddMembershipCommand(Guid UserId, Guid DepartmentId, string Role) : ICommand<MembershipDto>;
