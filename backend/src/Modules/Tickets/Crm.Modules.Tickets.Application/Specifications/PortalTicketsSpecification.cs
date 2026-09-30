using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application.Specifications;

/// <summary>The portal customer tickets (Story 47, D4/D9/D11). The scope filter already limits to the tenant and customer
/// of the grant; this adds the contact rule (a contact who is not the account admin sees only their own) and the list
/// criteria. Tracked when a command loads the aggregate; read-only otherwise. Paging only when both page arguments are
/// given (the count passes neither).</summary>
public sealed class PortalTicketsSpecification : Specification<Ticket>
{
    public PortalTicketsSpecification(
        PortalCustomerGrant grant, TicketId? id, IReadOnlyList<TicketStatus> statuses, string? search,
        int? page, int? pageSize, bool tracked)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(statuses);

        Guid customerId = grant.CustomerId;
        Guid? contactId = grant.IsAccountAdmin ? null : grant.ContactId;
        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        bool anyStatus = statuses.Count == 0;

        // One combined Where - Specification.Where replaces.
        Where(t => t.CustomerId == customerId
            && (contactId == null || t.ContactId == contactId)
            && (id == null || t.Id == id)
            && (anyStatus || statuses.Contains(t.Status))
            && (term == null || t.Number.Value.Contains(term) || t.Subject.Contains(term)));

        ApplyOrderByDescending(t => t.ModifiedAtUtc ?? t.CreatedAtUtc ?? DateTimeOffset.MinValue);
        ApplyThenBy(t => t.Number.Value);

        if (page is { } p && pageSize is { } size)
        {
            ApplyPaging((p - 1) * size, size);
        }

        if (!tracked)
        {
            AsReadOnly();
        }
    }
}
