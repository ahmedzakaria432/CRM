using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.DeactivateCategory;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record DeactivateCategoryCommand(Guid Id, byte[] RowVersion) : ICommand<TicketCategoryDto>;
