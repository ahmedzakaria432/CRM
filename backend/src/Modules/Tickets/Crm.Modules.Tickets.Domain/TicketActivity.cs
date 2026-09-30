using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Domain;

/// <summary>Child of Ticket - no DbSet, no repository, reached only through Ticket.Activities. A kind plus machine
/// parameters, never rendered text: the UI turns Kind/From/To into either language (Story 18).</summary>
public sealed class TicketActivity : Entity<Guid>
{
    public const int ValueMaxLength = 100;

    public TicketActivityKind Kind { get; private set; }

    public Guid ActorId { get; private set; }

    public string? From { get; private set; }

    public string? To { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private TicketActivity()
    {
    }

    internal TicketActivity(Guid id, TicketActivityKind kind, Guid actorId, string? from, string? to, DateTimeOffset occurredAtUtc)
        : base(id)
    {
        Kind = kind;
        ActorId = actorId;
        From = from;
        To = to;
        OccurredAtUtc = occurredAtUtc;
    }
}
