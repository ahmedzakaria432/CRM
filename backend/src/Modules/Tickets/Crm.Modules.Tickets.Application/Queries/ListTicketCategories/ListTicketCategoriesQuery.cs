using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListTicketCategories;

/// <summary>The agent picker: active categories only, of the given department or of every department in the caller's scope.</summary>
[RequirePermission(CrmPermissions.TicketsRead)]
public sealed record ListTicketCategoriesQuery(Guid? DepartmentId) : IRequest<Result<IReadOnlyList<TicketCategoryDto>>>;
