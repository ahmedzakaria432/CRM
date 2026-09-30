using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.RemoveMembership;

public sealed record RemoveMembershipCommand(Guid Id) : ICommand;
