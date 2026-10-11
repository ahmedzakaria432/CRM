using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Tests.Support;

/// <summary>Read-model rows (what the Infrastructure projections hand to the query handlers).</summary>
public static class Rows
{
    public static TicketListRow List(
        Guid? id = null, string number = "T-000001", string subject = "Printer is down", TicketStatus status = TicketStatus.New,
        TicketPriority priority = TicketPriority.Normal, TicketChannel channel = TicketChannel.Agent, Guid? category = null,
        Guid? customer = null, Guid? contact = null, Guid? agent = null, DateTimeOffset? created = null, DateTimeOffset? modified = null) =>
        new(
            id ?? Ids.Ticket1, number, subject, status, priority, channel, category, customer ?? Ids.Customer1, contact, agent,
            Ids.BranchA1.Value, Ids.DepartmentA1.Value, created ?? Clock.Now, modified);

    public static PortalTicketRow Portal(
        Guid? id = null, string number = "T-000001", string subject = "Printer is down", string description = "It is broken.",
        TicketStatus status = TicketStatus.New, Guid? category = null, Guid? contact = null, DateTimeOffset? created = null,
        DateTimeOffset? modified = null, DateTimeOffset? resolved = null, DateTimeOffset? closed = null) =>
        new(id ?? Ids.Ticket1, number, subject, description, status, category, contact, created ?? Clock.Now, modified, resolved, closed);

    public static TicketThreadRow Message(
        int sequence, TicketMessageDirection direction = TicketMessageDirection.Outbound, Guid? author = null, Guid? recordedBy = null,
        string body = "Hello", Guid? id = null) =>
        new(
            sequence, id ?? Guid.NewGuid(), TicketThreadVisibility.Public, Clock.Later(sequence), author, direction, recordedBy, body,
            null, null, null);

    public static TicketThreadRow Note(int sequence, Guid? author = null, string body = "Private", Guid? id = null) =>
        new(sequence, id ?? Guid.NewGuid(), TicketThreadVisibility.Internal, Clock.Later(sequence), author, null, null, body, null, null, null);

    public static TicketThreadRow Activity(
        int sequence, TicketActivityKind kind, Guid? actor = null, string? from = null, string? to = null, Guid? id = null) =>
        new(sequence, id ?? Guid.NewGuid(), TicketThreadVisibility.System, Clock.Later(sequence), actor, null, null, null, kind, from, to);
}
