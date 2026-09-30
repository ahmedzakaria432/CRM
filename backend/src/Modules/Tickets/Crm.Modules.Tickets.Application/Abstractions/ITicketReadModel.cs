using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application.Abstractions;

/// <summary>Queue and search projections (CLAUDE.md: queries project straight to DTOs). The specification supplies
/// criteria, order and page; the implementation projects with .Select, so the owned activities are never loaded.
/// Story 06's filters still apply - the implementation queries the module DbContext.</summary>
public interface ITicketReadModel
{
    Task<IReadOnlyList<TicketListRow>> ListAsync(Specification<Ticket> specification, CancellationToken cancellationToken = default);
}

public sealed record TicketListRow(
    Guid Id, string Number, string Subject, TicketStatus Status, TicketPriority Priority, TicketChannel Channel,
    Guid? CategoryId, Guid CustomerId, Guid? ContactId, Guid? AssignedAgentId, Guid BranchId, Guid DepartmentId,
    DateTimeOffset? CreatedAtUtc, DateTimeOffset? ModifiedAtUtc);
