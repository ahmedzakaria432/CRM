using Crm.BuildingBlocks.Application.Specifications;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Tickets.Infrastructure.Persistence;

/// <summary>Story 47: the customer-safe projection. It names no assignee, department, branch, priority, rowVersion, note or
/// activity, so none can reach a customer through it (S7). The Story 06 filters apply, including the portal customer clause.</summary>
public sealed class PortalTicketReadModel(TicketsDbContext context) : IPortalTicketReadModel
{
    public async Task<IReadOnlyList<PortalTicketRow>> ListAsync(Specification<Ticket> specification, CancellationToken cancellationToken = default) =>
        await SpecificationEvaluator.GetQuery(context.Tickets, specification)
            .Select(t => new PortalTicketRow(
                t.Id.Value, t.Number.Value, t.Subject, t.Description, t.Status,
                t.CategoryId == null ? null : t.CategoryId.Value,
                t.ContactId, t.CreatedAtUtc, t.ModifiedAtUtc, t.ResolvedAtUtc, t.ClosedAtUtc))
            .ToListAsync(cancellationToken);
}
