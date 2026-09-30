using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.CreateBranch;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record CreateBranchCommand(string Code, string NameAr, string NameEn) : ICommand<BranchDto>;
