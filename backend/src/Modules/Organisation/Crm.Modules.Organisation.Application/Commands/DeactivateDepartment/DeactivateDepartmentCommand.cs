using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.DeactivateDepartment;

public sealed record DeactivateDepartmentCommand(Guid Id, byte[] RowVersion) : ICommand<DepartmentDto>;
