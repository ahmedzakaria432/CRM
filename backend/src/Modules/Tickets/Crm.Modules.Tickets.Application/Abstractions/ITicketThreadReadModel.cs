using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application.Abstractions;

public enum TicketThreadVisibility
{
    /// <summary>Messages - what the customer may see.</summary>
    Public,
    /// <summary>Internal notes - staff only.</summary>
    Internal,
    /// <summary>Activities (history) - staff only.</summary>
    System,
}

/// <summary>The ticket thread (Story 18), projected straight from the owned tables with the Story 06 scope and
/// soft-delete filters of Tickets applied. Both methods return null when the ticket is not visible (unknown, deleted
/// or out of scope) and at most take rows rows with Sequence &gt; afterSequence, ascending.</summary>
public interface ITicketThreadReadModel
{
    Task<IReadOnlyList<TicketThreadRow>?> ListAsync(
        TicketId ticketId, IReadOnlyCollection<TicketThreadVisibility> visibilities, int afterSequence, int take,
        CancellationToken cancellationToken = default);

    /// <summary>Messages only - its query never names notes or activities. The ONLY method a customer-facing
    /// (portal, Story 47) query may call.</summary>
    Task<IReadOnlyList<TicketThreadRow>?> ListPublicAsync(
        TicketId ticketId, int afterSequence, int take, CancellationToken cancellationToken = default);
}

/// <summary>One thread entry. Messages: Direction, AuthorId, RecordedById, Body. Notes: AuthorId, Body. Activities:
/// AuthorId (the actor), Kind, From, To.</summary>
public sealed record TicketThreadRow(
    int Sequence, Guid Id, TicketThreadVisibility Visibility, DateTimeOffset OccurredAtUtc, Guid? AuthorId,
    TicketMessageDirection? Direction, Guid? RecordedById, string? Body, TicketActivityKind? Kind, string? From, string? To);
