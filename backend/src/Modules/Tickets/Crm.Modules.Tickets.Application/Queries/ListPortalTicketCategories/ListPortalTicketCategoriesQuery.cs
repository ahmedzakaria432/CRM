using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListPortalTicketCategories;

/// <summary>The categories the portal customer may pick: active and customer-visible, of the customer home department
/// (Story 47).</summary>
[RequirePermission(CrmPermissions.PortalTickets)]
public sealed record ListPortalTicketCategoriesQuery : IRequest<Result<IReadOnlyList<PortalTicketCategoryDto>>>;
