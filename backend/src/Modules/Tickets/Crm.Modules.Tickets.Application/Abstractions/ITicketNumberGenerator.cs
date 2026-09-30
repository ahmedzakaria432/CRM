using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application.Abstractions;

/// <summary>Draws the next value of the tickets.TicketNumbers sequence. Not transactional: a rolled-back create
/// leaves a gap.</summary>
public interface ITicketNumberGenerator
{
    Task<TicketNumber> NextAsync(CancellationToken cancellationToken = default);
}
