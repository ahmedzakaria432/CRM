using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.UpdateCategory;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record UpdateCategoryCommand(
    Guid Id, string NameAr, string NameEn, int DisplayOrder, bool IsCustomerVisible, byte[] RowVersion) : ICommand<TicketCategoryDto>;
