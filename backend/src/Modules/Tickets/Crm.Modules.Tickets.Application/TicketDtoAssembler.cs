using System.Globalization;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Application;

/// <summary>Builds the ticket DTOs, including the nested module read (Identity display names). Every handler calls it
/// BEFORE eventBus.Publish: a nested query after Publish throws (TransactionBehavior, R2).</summary>
public sealed class TicketDtoAssembler(IRepository<Category, CategoryId> categories, IIdentityModuleApi identity)
{
    public async Task<TicketDto> ToDtoAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        Category? category = ticket.CategoryId is { } categoryId
            ? await categories.GetByIdAsync(categoryId, cancellationToken)
            : null;

        List<TicketActivity> activities = [.. ticket.Activities.OrderBy(a => a.OccurredAtUtc)];

        HashSet<Guid> personIds = [];
        if (ticket.AssignedAgentId is { } assigned)
        {
            personIds.Add(assigned);
        }

        foreach (TicketActivity activity in activities)
        {
            personIds.Add(activity.ActorId);
            if (activity.Kind is TicketActivityKind.Assigned or TicketActivityKind.Claimed or TicketActivityKind.Unassigned)
            {
                AddIfGuid(personIds, activity.From);
                AddIfGuid(personIds, activity.To);
            }
        }

        IReadOnlyList<StaffUserSummary> people = await LookupAsync(personIds, cancellationToken);
        string? assignedName = ticket.AssignedAgentId is { } agentId ? people.FirstOrDefault(p => p.Id == agentId)?.DisplayName : null;

        return new TicketDto(
            ticket.Id.Value, ticket.Number.Value, ticket.Subject, ticket.Description, ticket.Status.ToString(),
            ticket.Priority.ToString(), ticket.Channel.ToString(), ticket.CustomerId, ticket.ContactId, ToRef(category),
            ticket.AssignedAgentId, assignedName, ticket.TenantId.Value, ticket.BranchId.Value, ticket.DepartmentId.Value,
            ticket.CreatedAtUtc, ticket.CreatedBy, ticket.ModifiedAtUtc, ticket.FirstRespondedAtUtc, ticket.ResolvedAtUtc,
            ticket.ClosedAtUtc,
            [.. ticket.AllowedActions().Select(a => a.ToString())],
            [.. activities.Select(a => new TicketActivityDto(a.Id, a.Kind.ToString(), a.ActorId, a.From, a.To, a.OccurredAtUtc))],
            [.. people.Select(p => new TicketPersonDto(p.Id, p.DisplayName))],
            ticket.RowVersion);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> ToListItemsAsync(IReadOnlyList<TicketListRow> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);

        List<CategoryId> categoryIds = [.. rows.Select(r => r.CategoryId).OfType<Guid>().Distinct().Select(id => new CategoryId(id))];
        IReadOnlyList<Category> found = categoryIds.Count == 0
            ? []
            : await categories.ListAsync(new CategoriesByIdsSpecification(categoryIds), cancellationToken);
        Dictionary<Guid, Category> byId = found.ToDictionary(c => c.Id.Value);

        HashSet<Guid> agentIds = [.. rows.Select(r => r.AssignedAgentId).OfType<Guid>()];
        IReadOnlyList<StaffUserSummary> people = await LookupAsync(agentIds, cancellationToken);

        return
        [
            .. rows.Select(r => new TicketListItemDto(
                r.Id, r.Number, r.Subject, r.Status.ToString(), r.Priority.ToString(), r.Channel.ToString(), r.CustomerId,
                r.ContactId,
                r.CategoryId is { } cid && byId.TryGetValue(cid, out Category? category) ? ToRef(category) : null,
                r.AssignedAgentId,
                r.AssignedAgentId is { } aid ? people.FirstOrDefault(p => p.Id == aid)?.DisplayName : null,
                r.BranchId, r.DepartmentId, r.CreatedAtUtc, r.ModifiedAtUtc)),
        ];
    }

    private async Task<IReadOnlyList<StaffUserSummary>> LookupAsync(HashSet<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        Result<IReadOnlyList<StaffUserSummary>> result = await identity.GetStaffUsersAsync(ids, cancellationToken);
        return result.IsSuccess ? result.Value : [];
    }

    private static TicketCategoryRefDto? ToRef(Category? category) =>
        category is null ? null : new TicketCategoryRefDto(category.Id.Value, TicketCategoryNameDto.FromDomain(category.Name), category.IsActive);

    private static void AddIfGuid(HashSet<Guid> ids, string? value)
    {
        if (Guid.TryParse(value, CultureInfo.InvariantCulture, out Guid id))
        {
            ids.Add(id);
        }
    }
}
