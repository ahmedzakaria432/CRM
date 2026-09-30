using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.RenameDepartment;

public sealed record RenameDepartmentCommand(Guid Id, string NameAr, string NameEn, byte[] RowVersion) : ICommand<DepartmentDto>;
