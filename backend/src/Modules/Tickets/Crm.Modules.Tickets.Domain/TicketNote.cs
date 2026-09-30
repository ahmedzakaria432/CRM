using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Domain;

/// <summary>An internal note (Story 18): visible to staff only, never returned by the public thread path.
/// Child of Ticket - no DbSet, no repository, reached only through Ticket.Notes. Plain text.</summary>
public sealed class TicketNote : Entity<Guid>
{
    /// <summary>Position in the ticket's thread, shared with messages and activities (Ticket.Revision at append).</summary>
    public int Sequence { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private TicketNote()
    {
        Body = string.Empty;
    }

    internal TicketNote(Guid id, int sequence, Guid authorId, string body, DateTimeOffset occurredAtUtc) : base(id)
    {
        Sequence = sequence;
        AuthorId = authorId;
        Body = body;
        OccurredAtUtc = occurredAtUtc;
    }
}
