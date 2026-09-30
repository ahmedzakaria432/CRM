using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.CreateCategory;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record CreateCategoryCommand(Guid DepartmentId, string NameAr, string NameEn, int DisplayOrder, bool IsCustomerVisible)
    : ICommand<TicketCategoryDto>;
