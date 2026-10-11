using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class TicketConversationTests
{
    [Fact]
    public void AnAgentReplyIsAnOutboundMessageByTheAgent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        int revision = ticket.Revision;

        TicketMessage message = ticket.ReplyAsAgent("  We are on it.  ", Ids.Agent1, Clock.Later(2));

        Assert.Equal(TicketMessageDirection.Outbound, message.Direction);
        Assert.Equal(Ids.Agent1, message.AuthorId);
        Assert.Equal(Ids.Agent1, message.RecordedById);
        Assert.Equal("We are on it.", message.Body);
        Assert.Equal(Clock.Later(2), message.OccurredAtUtc);
        Assert.Equal(revision + 1, message.Sequence);
        Assert.Equal(revision + 1, ticket.Revision);
        Assert.Same(message, Assert.Single(ticket.Messages));
        Assert.NotEqual(Guid.Empty, message.Id);
    }

    [Fact]
    public void AnAgentReplyRaisesOneMessageAddedEvent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketMessage message = ticket.ReplyAsAgent("Hello", Ids.Agent1, Clock.Later(2));

        TicketMessageAdded raised = Assert.IsType<TicketMessageAdded>(Assert.Single(ticket.DomainEvents));
        Assert.Equal(message.Id, raised.MessageId);
        Assert.Equal(TicketMessageDirection.Outbound, raised.Direction);
        Assert.Equal(Clock.Later(2), raised.OccurredOnUtc);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.PendingCustomer)]
    [InlineData(TicketStatus.OnHold)]
    [InlineData(TicketStatus.Resolved)]
    public void TheFirstAgentReplyInAnyAcceptingStatusSetsFirstRespondedAt(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        ticket.ReplyAsAgent("Hello", Ids.Agent1, Clock.Later(7));

        Assert.Equal(Clock.Later(7), ticket.FirstRespondedAtUtc);
    }

    [Fact]
    public void FirstRespondedAtNeverMovesAfterwards()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        ticket.ReplyAsAgent("one", Ids.Agent1, Clock.Later(1));

        ticket.ReplyAsAgent("two", Ids.Agent1, Clock.Later(9));
        ticket.ReceiveCustomerReply("three", Ids.Contact1, Ids.Agent1, Clock.Later(10));
        ticket.AddInternalNote("four", Ids.Agent1, Clock.Later(11));

        Assert.Equal(Clock.Later(1), ticket.FirstRespondedAtUtc);
    }

    [Fact]
    public void CustomerRepliesAndNotesNeverSetFirstRespondedAt()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        ticket.ReceiveCustomerReply("hi", Ids.Contact1, Ids.Agent1, Clock.Later(1));
        ticket.AddInternalNote("note", Ids.Agent1, Clock.Later(2));

        Assert.Null(ticket.FirstRespondedAtUtc);
    }

    [Fact]
    public void AReplyOnANewTicketDoesNotOpenIt()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.New);

        ticket.ReplyAsAgent("Hello", Ids.Agent1, Clock.Later(1));

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Single(ticket.Messages);
        Assert.Single(ticket.Activities);
    }

    [Fact]
    public void AReplyOnAResolvedTicketAppendsTheMessageThenAReopenedActivity()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Resolved);
        int revision = ticket.Revision;

        TicketMessage message = ticket.ReplyAsAgent("Sorry, back to you.", Ids.Agent1, Clock.Later(4));

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Equal(revision + 1, message.Sequence);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(TicketActivityKind.Reopened, activity.Kind);
        Assert.Equal(revision + 2, activity.Sequence);
        Assert.Equal(revision + 2, ticket.Revision);
        Assert.Collection(
            ticket.DomainEvents,
            e => Assert.IsType<TicketMessageAdded>(e),
            e => Assert.IsType<TicketStatusChanged>(e));
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void AReplyOnAFinishedTicketIsRefusedAndNothingIsAppended(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);
        int revision = ticket.Revision;

        Expect.DomainCode(() => ticket.ReplyAsAgent("Hello", Ids.Agent1, Clock.Now), ErrorCodes.TransitionNotAllowed);

        Assert.Equal(revision, ticket.Revision);
        Assert.Empty(ticket.Messages);
        Assert.Empty(ticket.DomainEvents);
        Assert.Null(ticket.FirstRespondedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void ABlankReplyBodyIsRefused(string body)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        int revision = ticket.Revision;

        Expect.DomainCode(() => ticket.ReplyAsAgent(body, Ids.Agent1, Clock.Now), ErrorCodes.BodyRequired);

        Assert.Equal(revision, ticket.Revision);
    }

    [Fact]
    public void ABodyOfTenThousandCharactersIsAccepted()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketMessage message = ticket.ReplyAsAgent(new string('b', 10_000), Ids.Agent1, Clock.Now);

        Assert.Equal(10_000, message.Body.Length);
    }

    [Fact]
    public void ABodyOfTenThousandAndOneCharactersIsRefusedWithoutChangingTheRevision()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        int revision = ticket.Revision;

        Expect.DomainCode(() => ticket.ReplyAsAgent(new string('b', 10_001), Ids.Agent1, Clock.Now), ErrorCodes.BodyTooLong);

        Assert.Equal(revision, ticket.Revision);
    }

    [Fact]
    public void TheBodyLengthIsCheckedAfterTheTrim()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketMessage message = ticket.ReplyAsAgent("  " + new string('b', 10_000) + "  ", Ids.Agent1, Clock.Now);

        Assert.Equal(10_000, message.Body.Length);
    }

    [Fact]
    public void ANullBodyIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Func<string, Guid, DateTimeOffset, TicketMessage> reply = ticket.ReplyAsAgent;
        Func<string, Guid?, Guid, DateTimeOffset, TicketMessage> receive = ticket.ReceiveCustomerReply;
        Func<string, Guid, DateTimeOffset, TicketNote> note = ticket.AddInternalNote;

        NullArguments.AssertThrowsArgumentNull(reply, null, Ids.Agent1, Clock.Now);
        NullArguments.AssertThrowsArgumentNull(receive, null, null, Ids.Agent1, Clock.Now);
        NullArguments.AssertThrowsArgumentNull(note, null, Ids.Agent1, Clock.Now);
    }

    [Fact]
    public void MarkupAndLineBreaksAreStoredVerbatim()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketMessage message = ticket.ReplyAsAgent("<script>alert(1)</script>\r\nsecond line & <b>bold</b>", Ids.Agent1, Clock.Now);

        Assert.Equal("<script>alert(1)</script>\r\nsecond line & <b>bold</b>", message.Body);
    }

    [Fact]
    public void AnArabicBodyIsStored()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketMessage message = ticket.ReplyAsAgent("نعمل على حل المشكلة", Ids.Agent1, Clock.Now);

        Assert.Equal("نعمل على حل المشكلة", message.Body);
    }

    [Fact]
    public void AnEmptyAgentIsRefusedOnAReply()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.ReplyAsAgent("Hello", Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
    }

    [Fact]
    public void ACustomerReplyIsAnInboundMessageWrittenByTheContactAndRecordedByTheActor()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketMessage message = ticket.ReceiveCustomerReply("  It still fails.  ", Ids.Contact1, Ids.Agent1, Clock.Later(3));

        Assert.Equal(TicketMessageDirection.Inbound, message.Direction);
        Assert.Equal(Ids.Contact1, message.AuthorId);
        Assert.Equal(Ids.Agent1, message.RecordedById);
        Assert.Equal("It still fails.", message.Body);
        TicketMessageAdded raised = Assert.IsType<TicketMessageAdded>(Assert.Single(ticket.DomainEvents));
        Assert.Equal(TicketMessageDirection.Inbound, raised.Direction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACustomerReplyWithoutAContactHasNoAuthor(bool emptyGuid)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        TicketMessage message = ticket.ReceiveCustomerReply("hi", emptyGuid ? Guid.Empty : null, Ids.Agent1, Clock.Now);

        Assert.Null(message.AuthorId);
        Assert.Equal(Ids.Agent1, message.RecordedById);
    }

    [Fact]
    public void ACustomerReplyOnAPendingCustomerTicketOpensIt()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.PendingCustomer);

        ticket.ReceiveCustomerReply("here", Ids.Contact1, Ids.Agent1, Clock.Later(2));

        Assert.Equal(TicketStatus.Open, ticket.Status);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(TicketActivityKind.Opened, activity.Kind);
        Assert.Equal("PendingCustomer", activity.From);
        Assert.Collection(
            ticket.DomainEvents,
            e => Assert.IsType<TicketMessageAdded>(e),
            e => Assert.IsType<TicketStatusChanged>(e));
    }

    [Fact]
    public void ACustomerReplyOnAResolvedTicketReopensIt()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Resolved);

        ticket.ReceiveCustomerReply("not solved", Ids.Contact1, Ids.Agent1, Clock.Later(2));

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketActivityKind.Reopened, ticket.Activities.Last().Kind);
        Assert.Null(ticket.ResolvedAtUtc);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.OnHold)]
    public void ACustomerReplyLeavesOtherWorkingStatusesAlone(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);
        int activities = ticket.Activities.Count;

        ticket.ReceiveCustomerReply("hi", Ids.Contact1, Ids.Agent1, Clock.Now);

        Assert.Equal(status, ticket.Status);
        Assert.Equal(activities, ticket.Activities.Count);
        Assert.Single(ticket.Messages);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void ACustomerReplyOnAFinishedTicketIsRefused(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        Expect.DomainCode(() => ticket.ReceiveCustomerReply("hi", Ids.Contact1, Ids.Agent1, Clock.Now), ErrorCodes.TransitionNotAllowed);
        Assert.Empty(ticket.Messages);
    }

    [Fact]
    public void ACustomerReplyByAnEmptyActorIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.ReceiveCustomerReply("hi", Ids.Contact1, Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.PendingCustomer)]
    [InlineData(TicketStatus.OnHold)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void AnInternalNoteIsAllowedInEveryStatusAndNeverChangesIt(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        TicketNote note = ticket.AddInternalNote("  private  ", Ids.Agent1, Clock.Later(2));

        Assert.Equal(status, ticket.Status);
        Assert.Equal("private", note.Body);
        Assert.Equal(Ids.Agent1, note.AuthorId);
        Assert.Equal(Clock.Later(2), note.OccurredAtUtc);
        Assert.Same(note, Assert.Single(ticket.Notes));
        TicketNoteAdded raised = Assert.IsType<TicketNoteAdded>(Assert.Single(ticket.DomainEvents));
        Assert.Equal(note.Id, raised.NoteId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ABlankNoteIsRefused(string body)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.AddInternalNote(body, Ids.Agent1, Clock.Now), ErrorCodes.BodyRequired);
    }

    [Fact]
    public void ANoteOfTenThousandAndOneCharactersIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.AddInternalNote(new string('n', 10_001), Ids.Agent1, Clock.Now), ErrorCodes.BodyTooLong);
        Assert.Empty(ticket.Notes);
    }

    [Fact]
    public void ANoteByAnEmptyAuthorIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.AddInternalNote("note", Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
    }

    [Fact]
    public void TheSequenceIsOneGapFreeKeyAcrossActivitiesMessagesAndNotes()
    {
        Ticket ticket = TicketFixtures.Create();

        ticket.AddInternalNote("n1", Ids.Agent1, Clock.Later(1));
        ticket.Open(Ids.Agent1, Clock.Later(2));
        ticket.ReplyAsAgent("m1", Ids.Agent1, Clock.Later(3));
        ticket.ReceiveCustomerReply("m2", Ids.Contact1, Ids.Agent1, Clock.Later(4));
        ticket.AssignTo(Ids.Agent1, Ids.Actor, Clock.Later(5));
        ticket.AddInternalNote("n2", Ids.Agent1, Clock.Later(6));
        ticket.Resolve(Ids.Agent1, Clock.Later(7));
        ticket.ReplyAsAgent("m3 reopens", Ids.Agent1, Clock.Later(8));

        List<int> sequences =
        [
            .. ticket.Activities.Select(a => a.Sequence),
            .. ticket.Messages.Select(m => m.Sequence),
            .. ticket.Notes.Select(n => n.Sequence),
        ];
        Assert.Equal(Enumerable.Range(1, sequences.Count), sequences.Order());
        Assert.Equal(sequences.Count, ticket.Revision);
        Assert.Equal(sequences.Count, sequences.Distinct().Count());
    }

    [Fact]
    public void ARefusedCallLeavesTheSequenceUnchanged()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Closed);
        int revision = ticket.Revision;

        Assert.Throws<DomainException>(() => ticket.ReplyAsAgent("x", Ids.Agent1, Clock.Now));
        Assert.Throws<DomainException>(() => ticket.AddInternalNote("", Ids.Agent1, Clock.Now));

        Assert.Equal(revision, ticket.Revision);
    }

    [Fact]
    public void MessagesNotesAndActivitiesAreReadOnlyViews()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Assert.False(ticket.Messages is ICollection<TicketMessage> { IsReadOnly: false });
        Assert.False(ticket.Notes is ICollection<TicketNote> { IsReadOnly: false });
        Assert.False(ticket.Activities is ICollection<TicketActivity> { IsReadOnly: false });
    }
}
