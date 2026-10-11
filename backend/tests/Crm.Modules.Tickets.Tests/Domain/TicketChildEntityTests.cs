using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Domain;

/// <summary>TicketMessage, TicketNote and TicketActivity have internal constructors: they are built, and read, only through Ticket.</summary>
public sealed class TicketChildEntityTests
{
    [Fact]
    public void AMessageMapsEveryField()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketMessage message = ticket.ReceiveCustomerReply("Hello", Ids.Contact1, Ids.Agent1, Clock.Later(5));

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(TicketMessageDirection.Inbound, message.Direction);
        Assert.Equal(Ids.Contact1, message.AuthorId);
        Assert.Equal(Ids.Agent1, message.RecordedById);
        Assert.Equal("Hello", message.Body);
        Assert.Equal(Clock.Later(5), message.OccurredAtUtc);
        Assert.Equal(ticket.Revision, message.Sequence);
    }

    [Fact]
    public void AnOutboundMessageIsAuthoredAndRecordedByTheAgent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketMessage message = ticket.ReplyAsAgent("Hello", Ids.Agent2, Clock.Now);

        Assert.Equal(Ids.Agent2, message.AuthorId);
        Assert.Equal(Ids.Agent2, message.RecordedById);
    }

    [Fact]
    public void ANoteMapsEveryField()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketNote note = ticket.AddInternalNote("Check the logs", Ids.Agent1, Clock.Later(5));

        Assert.NotEqual(Guid.Empty, note.Id);
        Assert.Equal(Ids.Agent1, note.AuthorId);
        Assert.Equal("Check the logs", note.Body);
        Assert.Equal(Clock.Later(5), note.OccurredAtUtc);
        Assert.Equal(ticket.Revision, note.Sequence);
    }

    [Fact]
    public void AnActivityMapsEveryField()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        ticket.PutOnHold(Ids.Agent1, Clock.Later(5));

        TicketActivity activity = ticket.Activities.Last();
        Assert.NotEqual(Guid.Empty, activity.Id);
        Assert.Equal(TicketActivityKind.PutOnHold, activity.Kind);
        Assert.Equal(Ids.Agent1, activity.ActorId);
        Assert.Equal("Open", activity.From);
        Assert.Equal("OnHold", activity.To);
        Assert.Equal(Clock.Later(5), activity.OccurredAtUtc);
        Assert.Equal(ticket.Revision, activity.Sequence);
    }

    [Fact]
    public void ActivityValuesFitTheStoredColumnWidth()
    {
        Assert.Equal(100, TicketActivity.ValueMaxLength);
        Assert.True(Guid.NewGuid().ToString().Length <= TicketActivity.ValueMaxLength);
        Assert.True(nameof(TicketStatus.PendingCustomer).Length <= TicketActivity.ValueMaxLength);
    }

    [Fact]
    public void EveryChildKeepsADistinctGeneratedId()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        ticket.ReplyAsAgent("a", Ids.Agent1, Clock.Now);
        ticket.ReplyAsAgent("b", Ids.Agent1, Clock.Now);
        ticket.AddInternalNote("c", Ids.Agent1, Clock.Now);

        List<Guid> ids = [.. ticket.Activities.Select(a => a.Id), .. ticket.Messages.Select(m => m.Id), .. ticket.Notes.Select(n => n.Id)];

        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void ChildrenAreReachedOnlyThroughTheirTicket()
    {
        Assert.True(typeof(TicketMessage).GetConstructors().Length == 0);
        Assert.True(typeof(TicketNote).GetConstructors().Length == 0);
        Assert.True(typeof(TicketActivity).GetConstructors().Length == 0);
    }
}
