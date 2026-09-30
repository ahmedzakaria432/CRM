using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListCategories;

/// <summary>The admin list: every category of the tenant (optionally one department, optionally active only).</summary>
[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record ListCategoriesQuery(Guid? DepartmentId, bool ActiveOnly) : IRequest<Result<IReadOnlyList<TicketCategoryDto>>>;
