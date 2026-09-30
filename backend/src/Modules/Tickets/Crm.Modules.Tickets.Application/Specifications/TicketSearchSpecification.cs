using System.Linq.Expressions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Application.Specifications;

/// <summary>Queues and search. No scope criteria on purpose - Story 06's filter adds tenant/branch/department.
/// Paging only when both page arguments are given (the count passes neither).</summary>
public sealed class TicketSearchSpecification : Specification<Ticket>
{
    public static readonly IReadOnlyList<TicketStatus> WorkingStatuses =
        [TicketStatus.New, TicketStatus.Open, TicketStatus.PendingCustomer, TicketStatus.OnHold];

    public TicketSearchSpecification(
        TicketQueueKind queue, Guid currentUserId, string? search, IReadOnlyList<TicketStatus> statuses,
        IReadOnlyList<TicketPriority> priorities, Guid? categoryId, Guid? assigneeId, Guid? departmentId, Guid? branchId,
        Guid? customerId, TicketSort sort, bool descending, int? page, int? pageSize)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentNullException.ThrowIfNull(priorities);

        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        IReadOnlyList<TicketStatus> effectiveStatuses = statuses.Count == 0 && queue != TicketQueueKind.All ? WorkingStatuses : statuses;
        bool anyStatus = effectiveStatuses.Count == 0;
        bool anyPriority = priorities.Count == 0;
        bool mine = queue == TicketQueueKind.Mine;
        bool unassigned = queue == TicketQueueKind.Unassigned;
        Guid? me = currentUserId;
        CategoryId? category = categoryId is { } c ? new CategoryId(c) : null;
        DepartmentId? department = departmentId is { } d ? new DepartmentId(d) : null;
        BranchId? branch = branchId is { } b ? new BranchId(b) : null;

        // One combined Where - Specification.Where replaces (DepartmentsSpecification precedent).
        Where(t =>
            (term == null || t.Number.Value.Contains(term) || t.Subject.Contains(term))
            && (anyStatus || effectiveStatuses.Contains(t.Status))
            && (anyPriority || priorities.Contains(t.Priority))
            && (!mine || t.AssignedAgentId == me)
            && (!unassigned || t.AssignedAgentId == null)
            && (assigneeId == null || t.AssignedAgentId == assigneeId)
            && (category == null || t.CategoryId == category)
            && (department == null || t.DepartmentId.Equals(department))
            && (branch == null || t.BranchId.Equals(branch))
            && (customerId == null || t.CustomerId == customerId));

        Expression<Func<Ticket, object>> key = sort switch
        {
            TicketSort.ModifiedAt => t => t.ModifiedAtUtc ?? DateTimeOffset.MinValue,
            TicketSort.Priority => t => t.Priority,
            TicketSort.Number => t => t.Number.Value,
            _ => t => t.CreatedAtUtc ?? DateTimeOffset.MinValue,
        };

        if (descending)
        {
            ApplyOrderByDescending(key);
        }
        else
        {
            ApplyOrderBy(key);
        }

        ApplyThenBy(t => t.Number.Value);

        if (page is { } p && pageSize is { } size)
        {
            ApplyPaging((p - 1) * size, size);
        }

        AsReadOnly();
    }
}
