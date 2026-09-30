using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.CreateBranch;

public sealed record CreateBranchCommand(string Code, string NameAr, string NameEn) : ICommand<BranchDto>;
