using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.ReactivateCategory;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record ReactivateCategoryCommand(Guid Id, byte[] RowVersion) : ICommand<TicketCategoryDto>;
