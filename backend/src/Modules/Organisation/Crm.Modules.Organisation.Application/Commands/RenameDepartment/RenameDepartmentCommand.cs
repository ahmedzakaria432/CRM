using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.RenameDepartment;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record RenameDepartmentCommand(Guid Id, string NameAr, string NameEn, byte[] RowVersion) : ICommand<DepartmentDto>;
