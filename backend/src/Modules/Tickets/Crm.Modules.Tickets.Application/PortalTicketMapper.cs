using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application;

/// <summary>The one place portal DTOs are built (Story 47, S7). It reads only the customer-safe members of a ticket or
/// row - never notes, activities, assignee, department, priority or rowVersion. Category names come from the
/// tenant-scoped Category rows.</summary>
public sealed class PortalTicketMapper(IRepository<Category, CategoryId> categories)
{
    public async Task<IReadOnlyList<PortalTicketSummaryDto>> ToSummariesAsync(
        IReadOnlyList<PortalTicketRow> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);

        Dictionary<Guid, PortalTicketCategoryDto> names = await CategoryNamesAsync(rows.Select(r => r.CategoryId), cancellationToken);

        return
        [
            .. rows.Select(r => new PortalTicketSummaryDto(
                r.Id, r.Number, r.Subject, PortalTicketStatuses.Name(r.Status), CategoryOf(names, r.CategoryId),
                r.CreatedAtUtc, r.ModifiedAtUtc ?? r.CreatedAtUtc)),
        ];
    }

    public async Task<PortalTicketDto> ToDtoAsync(PortalTicketRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        Dictionary<Guid, PortalTicketCategoryDto> names = await CategoryNamesAsync([row.CategoryId], cancellationToken);

        return new PortalTicketDto(
            row.Id, row.Number, row.Subject, row.Description, PortalTicketStatuses.Name(row.Status), CategoryOf(names, row.CategoryId),
            row.CreatedAtUtc, row.ModifiedAtUtc ?? row.CreatedAtUtc, row.ResolvedAtUtc, row.ClosedAtUtc,
            [.. Ticket.CustomerActionsFor(row.Status).Select(a => a.ToString())]);
    }

    /// <summary>For a command result. Reads only Id, Number, Subject, Description, Status, CategoryId and the four
    /// timestamps of the aggregate.</summary>
    public async Task<PortalTicketDto> ToDtoAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        Dictionary<Guid, PortalTicketCategoryDto> names = await CategoryNamesAsync([ticket.CategoryId?.Value], cancellationToken);

        return new PortalTicketDto(
            ticket.Id.Value, ticket.Number.Value, ticket.Subject, ticket.Description, PortalTicketStatuses.Name(ticket.Status),
            CategoryOf(names, ticket.CategoryId?.Value), ticket.CreatedAtUtc, ticket.ModifiedAtUtc ?? ticket.CreatedAtUtc,
            ticket.ResolvedAtUtc, ticket.ClosedAtUtc,
            [.. Ticket.CustomerActionsFor(ticket.Status).Select(a => a.ToString())]);
    }

    private static PortalTicketCategoryDto? CategoryOf(Dictionary<Guid, PortalTicketCategoryDto> names, Guid? categoryId) =>
        categoryId is { } id && names.TryGetValue(id, out PortalTicketCategoryDto? dto) ? dto : null;

    private async Task<Dictionary<Guid, PortalTicketCategoryDto>> CategoryNamesAsync(
        IEnumerable<Guid?> categoryIds, CancellationToken cancellationToken)
    {
        List<CategoryId> ids = [.. categoryIds.Where(c => c is not null).Select(c => new CategoryId(c ?? Guid.Empty)).Distinct()];

        if (ids.Count == 0)
        {
            return [];
        }

        IReadOnlyList<Category> found = await categories.ListAsync(new CategoriesByIdsSpecification(ids), cancellationToken);

        return found.ToDictionary(c => c.Id.Value, c => new PortalTicketCategoryDto(c.Id.Value, c.Name.Arabic, c.Name.English));
    }
}
