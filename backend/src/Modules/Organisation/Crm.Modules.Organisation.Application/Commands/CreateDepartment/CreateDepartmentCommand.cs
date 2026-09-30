using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.CreateDepartment;

public sealed record CreateDepartmentCommand(Guid BranchId, string Code, string NameAr, string NameEn) : ICommand<DepartmentDto>;
