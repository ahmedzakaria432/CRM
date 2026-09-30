using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.RenameBranch;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record RenameBranchCommand(Guid Id, string NameAr, string NameEn, byte[] RowVersion) : ICommand<BranchDto>;
