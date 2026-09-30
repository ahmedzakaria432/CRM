using Crm.BuildingBlocks.Application.Specifications;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Tickets.Infrastructure.Persistence;

public sealed class TicketReadModel(TicketsDbContext context) : ITicketReadModel
{
    public async Task<IReadOnlyList<TicketListRow>> ListAsync(Specification<Ticket> specification, CancellationToken cancellationToken = default) =>
        await SpecificationEvaluator.GetQuery(context.Tickets, specification)
            .Select(t => new TicketListRow(
                t.Id.Value, t.Number.Value, t.Subject, t.Status, t.Priority, t.Channel,
                t.CategoryId == null ? null : t.CategoryId.Value,
                t.CustomerId, t.ContactId, t.AssignedAgentId, t.BranchId.Value, t.DepartmentId.Value,
                t.CreatedAtUtc, t.ModifiedAtUtc))
            .ToListAsync(cancellationToken);
}
