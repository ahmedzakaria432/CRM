using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application.Abstractions;

/// <summary>Customer-safe projections (Story 47, D12/S7): the rows carry no assignee, department, branch, priority,
/// rowVersion, note or activity. The Story 06 filters (including the portal customer clause) apply.</summary>
public interface IPortalTicketReadModel
{
    Task<IReadOnlyList<PortalTicketRow>> ListAsync(Specification<Ticket> specification, CancellationToken cancellationToken = default);
}

public sealed record PortalTicketRow(
    Guid Id, string Number, string Subject, string Description, TicketStatus Status, Guid? CategoryId, Guid? ContactId,
    DateTimeOffset? CreatedAtUtc, DateTimeOffset? ModifiedAtUtc, DateTimeOffset? ResolvedAtUtc, DateTimeOffset? ClosedAtUtc);
