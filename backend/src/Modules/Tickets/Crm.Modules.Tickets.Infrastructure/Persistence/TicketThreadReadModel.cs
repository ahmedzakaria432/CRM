using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Tickets.Infrastructure.Persistence;

public sealed class TicketThreadReadModel(TicketsDbContext context) : ITicketThreadReadModel
{
    public async Task<IReadOnlyList<TicketThreadRow>?> ListAsync(
        TicketId ticketId, IReadOnlyCollection<TicketThreadVisibility> visibilities, int afterSequence, int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visibilities);
        bool messages = visibilities.Contains(TicketThreadVisibility.Public);
        bool notes = visibilities.Contains(TicketThreadVisibility.Internal);
        bool activities = visibilities.Contains(TicketThreadVisibility.System);

        var page = await context.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new
            {
                Messages = t.Messages.Where(m => messages && m.Sequence > afterSequence).OrderBy(m => m.Sequence).Take(take)
                    .Select(m => new TicketThreadRow(
                        m.Sequence, m.Id, TicketThreadVisibility.Public, m.OccurredAtUtc, m.AuthorId,
                        (TicketMessageDirection?)m.Direction, (Guid?)m.RecordedById, m.Body, null, null, null))
                    .ToList(),
                Notes = t.Notes.Where(n => notes && n.Sequence > afterSequence).OrderBy(n => n.Sequence).Take(take)
                    .Select(n => new TicketThreadRow(
                        n.Sequence, n.Id, TicketThreadVisibility.Internal, n.OccurredAtUtc, (Guid?)n.AuthorId,
                        null, null, n.Body, null, null, null))
                    .ToList(),
                Activities = t.Activities.Where(a => activities && a.Sequence > afterSequence).OrderBy(a => a.Sequence).Take(take)
                    .Select(a => new TicketThreadRow(
                        a.Sequence, a.Id, TicketThreadVisibility.System, a.OccurredAtUtc, (Guid?)a.ActorId,
                        null, null, null, (TicketActivityKind?)a.Kind, a.From, a.To))
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return page is null
            ? null
            : [.. page.Messages.Concat(page.Notes).Concat(page.Activities).OrderBy(r => r.Sequence).Take(take)];
    }

    public async Task<IReadOnlyList<TicketThreadRow>?> ListPublicAsync(
        TicketId ticketId, int afterSequence, int take, CancellationToken cancellationToken = default)
    {
        var page = await context.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new
            {
                Messages = t.Messages.Where(m => m.Sequence > afterSequence).OrderBy(m => m.Sequence).Take(take)
                    .Select(m => new TicketThreadRow(
                        m.Sequence, m.Id, TicketThreadVisibility.Public, m.OccurredAtUtc, m.AuthorId,
                        (TicketMessageDirection?)m.Direction, (Guid?)m.RecordedById, m.Body, null, null, null))
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return page?.Messages;
    }
}
