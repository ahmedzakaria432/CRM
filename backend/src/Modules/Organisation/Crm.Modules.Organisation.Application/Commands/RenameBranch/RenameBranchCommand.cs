using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.RenameBranch;

public sealed record RenameBranchCommand(Guid Id, string NameAr, string NameEn, byte[] RowVersion) : ICommand<BranchDto>;
