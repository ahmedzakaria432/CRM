using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Tickets.Application.Commands.ChangeTicketCategory;

[RequirePermission(CrmPermissions.TicketsWrite)]
public sealed record ChangeTicketCategoryCommand(Guid Id, Guid CategoryId, byte[] RowVersion) : ICommand<TicketDto>;
