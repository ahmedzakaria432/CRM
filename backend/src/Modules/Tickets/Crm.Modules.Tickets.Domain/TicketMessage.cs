using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Domain;

/// <summary>A public message of the ticket conversation (Story 18). Child of Ticket - no DbSet, no repository,
/// reached only through Ticket.Messages. Not IAuditable: OccurredAtUtc and RecordedById are domain data.
/// Plain text: never parsed or rendered as HTML.</summary>
public sealed class TicketMessage : Entity<Guid>
{
    /// <summary>Position in the ticket's thread, shared with notes and activities (Ticket.Revision at append).</summary>
    public int Sequence { get; private set; }

    public TicketMessageDirection Direction { get; private set; }

    /// <summary>Outbound: the agent's user id. Inbound: a contact id, or null for "the customer".</summary>
    public Guid? AuthorId { get; private set; }

    /// <summary>Who put the message into the CRM: the replying agent, or whoever recorded the customer's reply.</summary>
    public Guid RecordedById { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private TicketMessage()
    {
        Body = string.Empty;
    }

    internal TicketMessage(
        Guid id, int sequence, TicketMessageDirection direction, Guid? authorId, Guid recordedById, string body,
        DateTimeOffset occurredAtUtc) : base(id)
    {
        Sequence = sequence;
        Direction = direction;
        AuthorId = authorId;
        RecordedById = recordedById;
        Body = body;
        OccurredAtUtc = occurredAtUtc;
    }
}
