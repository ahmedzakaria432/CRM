using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class TicketStatusTransitionTests
{
    private static readonly TicketAction[] StatusActions =
    [
        TicketAction.Open, TicketAction.AwaitCustomer, TicketAction.PutOnHold, TicketAction.Resolve, TicketAction.Close,
        TicketAction.Reopen, TicketAction.Cancel,
    ];

    private static readonly Dictionary<TicketAction, (TicketStatus[] From, TicketStatus To, TicketActivityKind Kind)> Table = new()
    {
        [TicketAction.Open] = ([TicketStatus.New, TicketStatus.PendingCustomer, TicketStatus.OnHold], TicketStatus.Open, TicketActivityKind.Opened),
        [TicketAction.AwaitCustomer] = ([TicketStatus.Open, TicketStatus.OnHold], TicketStatus.PendingCustomer, TicketActivityKind.AwaitingCustomer),
        [TicketAction.PutOnHold] = ([TicketStatus.Open, TicketStatus.PendingCustomer], TicketStatus.OnHold, TicketActivityKind.PutOnHold),
        [TicketAction.Resolve] = ([TicketStatus.New, TicketStatus.Open, TicketStatus.PendingCustomer, TicketStatus.OnHold], TicketStatus.Resolved, TicketActivityKind.Resolved),
        [TicketAction.Close] = ([TicketStatus.Resolved], TicketStatus.Closed, TicketActivityKind.Closed),
        [TicketAction.Reopen] = ([TicketStatus.Resolved, TicketStatus.Closed], TicketStatus.Open, TicketActivityKind.Reopened),
        [TicketAction.Cancel] = ([TicketStatus.New, TicketStatus.Open, TicketStatus.PendingCustomer, TicketStatus.OnHold], TicketStatus.Cancelled, TicketActivityKind.Cancelled),
    };

    public static TheoryData<TicketStatus, TicketAction> LegalPairs()
    {
        TheoryData<TicketStatus, TicketAction> data = [];
        foreach (TicketAction action in StatusActions)
        {
            foreach (TicketStatus from in Table[action].From)
            {
                data.Add(from, action);
            }
        }

        return data;
    }

    public static TheoryData<TicketStatus, TicketAction> IllegalPairs()
    {
        TheoryData<TicketStatus, TicketAction> data = [];
        foreach (TicketAction action in StatusActions)
        {
            foreach (TicketStatus status in Enum.GetValues<TicketStatus>().Where(s => !Table[action].From.Contains(s)))
            {
                data.Add(status, action);
            }
        }

        return data;
    }

    private static void Perform(Ticket ticket, TicketAction action, Guid actor, DateTimeOffset at)
    {
        switch (action)
        {
            case TicketAction.Open:
                ticket.Open(actor, at);
                break;
            case TicketAction.AwaitCustomer:
                ticket.AwaitCustomer(actor, at);
                break;
            case TicketAction.PutOnHold:
                ticket.PutOnHold(actor, at);
                break;
            case TicketAction.Resolve:
                ticket.Resolve(actor, at);
                break;
            case TicketAction.Close:
                ticket.Close(actor, at);
                break;
            case TicketAction.Reopen:
                ticket.Reopen(actor, at);
                break;
            case TicketAction.Cancel:
                ticket.Cancel(actor, at);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    [Fact]
    public void TheMatrixCoversFortyNineCombinations()
    {
        Assert.Equal(49, LegalPairs().Count + IllegalPairs().Count);
    }

    [Theory]
    [MemberData(nameof(LegalPairs))]
    public void ALegalActionMovesTheStatusAndAppendsOneActivity(TicketStatus from, TicketAction action)
    {
        Ticket ticket = TicketFixtures.InStatus(from);
        int revision = ticket.Revision;
        int activities = ticket.Activities.Count;

        Perform(ticket, action, Ids.Agent1, Clock.Later(3));

        (TicketStatus[] _, TicketStatus to, TicketActivityKind kind) = Table[action];
        Assert.Equal(to, ticket.Status);
        Assert.Equal(revision + 1, ticket.Revision);
        Assert.Equal(activities + 1, ticket.Activities.Count);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(kind, activity.Kind);
        Assert.Equal(from.ToString(), activity.From);
        Assert.Equal(to.ToString(), activity.To);
        Assert.Equal(Ids.Agent1, activity.ActorId);
        Assert.Equal(Clock.Later(3), activity.OccurredAtUtc);
        Assert.Equal(ticket.Revision, activity.Sequence);
    }

    [Theory]
    [MemberData(nameof(LegalPairs))]
    public void ALegalActionRaisesOneStatusChangedEvent(TicketStatus from, TicketAction action)
    {
        Ticket ticket = TicketFixtures.InStatus(from);

        Perform(ticket, action, Ids.Agent1, Clock.Later(3));

        TicketStatusChanged raised = Assert.IsType<TicketStatusChanged>(Assert.Single(ticket.DomainEvents));
        Assert.Equal(from, raised.From);
        Assert.Equal(Table[action].To, raised.To);
        Assert.Equal(ticket.Id, raised.TicketId);
        Assert.Equal(Clock.Later(3), raised.OccurredOnUtc);
    }

    [Theory]
    [MemberData(nameof(IllegalPairs))]
    public void AnIllegalActionIsRefusedAndNothingChanges(TicketStatus from, TicketAction action)
    {
        Ticket ticket = TicketFixtures.InStatus(from);
        int revision = ticket.Revision;
        int activities = ticket.Activities.Count;

        var exception = Assert.Throws<DomainException>(() => Perform(ticket, action, Ids.Agent1, Clock.Later(3)));

        Assert.Equal(ErrorCodes.TransitionNotAllowed, exception.Error.Code);
        Assert.Equal($"'{action}' is not allowed while the ticket is {from}.", exception.Error.Message);
        Assert.Equal(from, ticket.Status);
        Assert.Equal(revision, ticket.Revision);
        Assert.Equal(activities, ticket.Activities.Count);
        Assert.Empty(ticket.DomainEvents);
    }

    [Fact]
    public void ResolveSetsTheResolvedTimestamp()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        ticket.Resolve(Ids.Agent1, Clock.Later(10));

        Assert.Equal(Clock.Later(10), ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    [Fact]
    public void CloseSetsTheClosedTimestampAndKeepsTheResolvedOne()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        ticket.Resolve(Ids.Agent1, Clock.Later(10));

        ticket.Close(Ids.Agent1, Clock.Later(20));

        Assert.Equal(Clock.Later(10), ticket.ResolvedAtUtc);
        Assert.Equal(Clock.Later(20), ticket.ClosedAtUtc);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void ReopenClearsBothTimestamps(TicketStatus from)
    {
        Ticket ticket = TicketFixtures.InStatus(from);
        Assert.NotNull(ticket.ResolvedAtUtc);

        ticket.Reopen(Ids.Agent1, Clock.Later(30));

        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    [Fact]
    public void CancelAndOpenLeaveTheTimestampsAlone()
    {
        Ticket cancelled = TicketFixtures.InStatus(TicketStatus.New);
        cancelled.Cancel(Ids.Agent1, Clock.Later(1));
        Ticket opened = TicketFixtures.InStatus(TicketStatus.New);
        opened.Open(Ids.Agent1, Clock.Later(1));

        Assert.Null(cancelled.ResolvedAtUtc);
        Assert.Null(cancelled.ClosedAtUtc);
        Assert.Null(opened.ResolvedAtUtc);
        Assert.Null(opened.ClosedAtUtc);
    }

    [Theory]
    [InlineData(TicketAction.Open)]
    [InlineData(TicketAction.AwaitCustomer)]
    [InlineData(TicketAction.PutOnHold)]
    [InlineData(TicketAction.Resolve)]
    [InlineData(TicketAction.Close)]
    [InlineData(TicketAction.Reopen)]
    [InlineData(TicketAction.Cancel)]
    public void AnEmptyActorIsRefusedBeforeTheTransitionIsChecked(TicketAction action)
    {
        // A Cancelled ticket refuses every action, so the actor guard is proved by the code it reports.
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Cancelled);

        Expect.DomainCode(() => Perform(ticket, action, Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
    }

    [Theory]
    [InlineData(TicketStatus.Cancelled)]
    [InlineData(TicketStatus.Closed)]
    public void CancelledAndClosedAreTerminalExceptClosedToReopen(TicketStatus terminal)
    {
        foreach (TicketAction action in StatusActions)
        {
            Ticket ticket = TicketFixtures.InStatus(terminal);
            bool allowed = terminal == TicketStatus.Closed && action == TicketAction.Reopen;

            if (allowed)
            {
                Perform(ticket, action, Ids.Agent1, Clock.Now);
                Assert.Equal(TicketStatus.Open, ticket.Status);
            }
            else
            {
                Expect.DomainCode(() => Perform(ticket, action, Ids.Agent1, Clock.Now), ErrorCodes.TransitionNotAllowed);
            }
        }
    }

    [Fact]
    public void ASequenceOfTransitionsWalksTheLifecycle()
    {
        Ticket ticket = TicketFixtures.Create();

        ticket.Open(Ids.Agent1, Clock.Later(1));
        ticket.AwaitCustomer(Ids.Agent1, Clock.Later(2));
        ticket.PutOnHold(Ids.Agent1, Clock.Later(3));
        ticket.Open(Ids.Agent1, Clock.Later(4));
        ticket.Resolve(Ids.Agent1, Clock.Later(5));
        ticket.Reopen(Ids.Agent1, Clock.Later(6));
        ticket.Resolve(Ids.Agent1, Clock.Later(7));
        ticket.Close(Ids.Agent1, Clock.Later(8));

        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.Equal(9, ticket.Revision);
        Assert.Equal(Enumerable.Range(1, 9), ticket.Activities.Select(a => a.Sequence));
    }
}
