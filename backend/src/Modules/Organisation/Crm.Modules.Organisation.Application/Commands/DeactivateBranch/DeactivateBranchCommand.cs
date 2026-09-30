using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.DeactivateBranch;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record DeactivateBranchCommand(Guid Id, byte[] RowVersion) : ICommand<BranchDto>;
