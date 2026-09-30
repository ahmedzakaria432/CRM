using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.CreateDepartment;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record CreateDepartmentCommand(Guid BranchId, string Code, string NameAr, string NameEn) : ICommand<DepartmentDto>;
