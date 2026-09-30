using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.ChangeMembershipRole;

public sealed record ChangeMembershipRoleCommand(Guid Id, string Role, byte[] RowVersion) : ICommand<MembershipDto>;
