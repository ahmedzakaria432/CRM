using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Domain;

/// <summary>Child of Ticket - no DbSet, no repository, reached only through Ticket.Activities. A kind plus machine
/// parameters, never rendered text: the UI turns Kind (the template key) and From/To (its parameters) into either
/// language (Story 18).</summary>
public sealed class TicketActivity : Entity<Guid>
{
    public const int ValueMaxLength = 100;

    /// <summary>Position in the ticket's thread (Story 18), shared with messages and notes.</summary>
    public int Sequence { get; private set; }

    public TicketActivityKind Kind { get; private set; }

    public Guid ActorId { get; private set; }

    public string? From { get; private set; }

    public string? To { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private TicketActivity()
    {
    }

    internal TicketActivity(Guid id, int sequence, TicketActivityKind kind, Guid actorId, string? from, string? to, DateTimeOffset occurredAtUtc)
        : base(id)
    {
        Sequence = sequence;
        Kind = kind;
        ActorId = actorId;
        From = from;
        To = to;
        OccurredAtUtc = occurredAtUtc;
    }
}
