using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.DeactivateBranch;

public sealed record DeactivateBranchCommand(Guid Id, byte[] RowVersion) : ICommand<BranchDto>;
