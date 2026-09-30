using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.DeactivateDepartment;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record DeactivateDepartmentCommand(Guid Id, byte[] RowVersion) : ICommand<DepartmentDto>;
