using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class TicketAllowedActionsTests
{
    private static readonly TicketStatus[] Working = [TicketStatus.New, TicketStatus.Open, TicketStatus.PendingCustomer, TicketStatus.OnHold];

    private static readonly Dictionary<TicketAction, TicketStatus[]> StatusActions = new()
    {
        [TicketAction.Open] = [TicketStatus.New, TicketStatus.PendingCustomer, TicketStatus.OnHold],
        [TicketAction.AwaitCustomer] = [TicketStatus.Open, TicketStatus.OnHold],
        [TicketAction.PutOnHold] = [TicketStatus.Open, TicketStatus.PendingCustomer],
        [TicketAction.Resolve] = Working,
        [TicketAction.Close] = [TicketStatus.Resolved],
        [TicketAction.Reopen] = [TicketStatus.Resolved, TicketStatus.Closed],
        [TicketAction.Cancel] = Working,
    };

    public static TheoryData<TicketStatus, TicketAction> Matrix()
    {
        TheoryData<TicketStatus, TicketAction> data = [];
        foreach (TicketStatus status in Enum.GetValues<TicketStatus>())
        {
            foreach (TicketAction action in Enum.GetValues<TicketAction>())
            {
                data.Add(status, action);
            }
        }

        return data;
    }

    private static bool Expected(TicketStatus status, TicketAction action) => action switch
    {
        TicketAction.Reply => status is not (TicketStatus.Closed or TicketStatus.Cancelled),
        TicketAction.AddNote => true,
        _ when StatusActions.TryGetValue(action, out TicketStatus[]? from) => from.Contains(status),
        _ => Working.Contains(status),
    };

    [Fact]
    public void TheMatrixHasOneHundredAndFiveRows()
    {
        Assert.Equal(105, Matrix().Count);
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void IsAllowedInCurrentStatusFollowsTheTable(TicketStatus status, TicketAction action)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        Assert.Equal(Expected(status, action), ticket.IsAllowedInCurrentStatus(action));
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void CanPerformAddsTheAssigneeRuleForUnassignedTickets(TicketStatus status, TicketAction action)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        bool expected = Expected(status, action) && action != TicketAction.Unassign;

        Assert.Equal(expected, ticket.CanPerform(action));
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void CanPerformAddsTheAssigneeRuleForAssignedTickets(TicketStatus status, TicketAction action)
    {
        Ticket ticket = TicketFixtures.InStatus(status);
        SetAssignee(ticket);

        bool expected = Expected(status, action) && action != TicketAction.Claim;

        Assert.Equal(expected, ticket.CanPerform(action));
    }

    // Assign is refused on a Closed/Resolved/Cancelled ticket, so the assignee of those is set while it is still working
    // by walking a fresh ticket: build the status after the assignment.
    private static void SetAssignee(Ticket ticket)
    {
        // Only used for statuses where Assign is allowed; for the others the test expects the status rule alone and
        // the ticket stays unassigned, which gives the same answer for every action except Unassign (not allowed there either).
        if (ticket.IsAllowedInCurrentStatus(TicketAction.Assign))
        {
            ticket.AssignTo(Ids.Agent1, Ids.Actor, Clock.Now);
        }
    }

    [Fact]
    public void IsAllowedInCurrentStatusIgnoresTheAssignee()
    {
        Ticket unassigned = TicketFixtures.InStatus(TicketStatus.Open);
        Ticket assigned = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);

        Assert.True(unassigned.IsAllowedInCurrentStatus(TicketAction.Claim));
        Assert.True(unassigned.IsAllowedInCurrentStatus(TicketAction.Unassign));
        Assert.True(assigned.IsAllowedInCurrentStatus(TicketAction.Claim));
        Assert.True(assigned.IsAllowedInCurrentStatus(TicketAction.Unassign));
    }

    [Fact]
    public void ClaimNeedsNoAssigneeAndUnassignNeedsOne()
    {
        Ticket unassigned = TicketFixtures.InStatus(TicketStatus.Open);
        Ticket assigned = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);

        Assert.True(unassigned.CanPerform(TicketAction.Claim));
        Assert.False(unassigned.CanPerform(TicketAction.Unassign));
        Assert.False(assigned.CanPerform(TicketAction.Claim));
        Assert.True(assigned.CanPerform(TicketAction.Unassign));
    }

    [Theory]
    [MemberData(nameof(StatusesOnly))]
    public void AllowedActionsIsExactlyTheCanPerformSetInEnumOrder(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        IReadOnlyList<TicketAction> actions = ticket.AllowedActions();

        Assert.Equal(Enum.GetValues<TicketAction>().Where(ticket.CanPerform), actions);
    }

    public static TheoryData<TicketStatus> StatusesOnly() => [.. Enum.GetValues<TicketStatus>()];

    [Fact]
    public void ANewUnassignedTicketOffersTheWorkingActionsAndNotesAndReplies()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.New);

        Assert.Equal(
            [
                TicketAction.Open, TicketAction.Resolve, TicketAction.Cancel, TicketAction.Assign, TicketAction.Claim,
                TicketAction.Transfer, TicketAction.ChangePriority, TicketAction.ChangeCategory, TicketAction.Reply, TicketAction.AddNote,
            ],
            ticket.AllowedActions());
    }

    [Fact]
    public void AClosedTicketOffersReopenAndNotesOnly()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Closed);

        Assert.Equal([TicketAction.Reopen, TicketAction.AddNote], ticket.AllowedActions());
    }

    [Theory]
    [InlineData(TicketStatus.New, true)]
    [InlineData(TicketStatus.Open, true)]
    [InlineData(TicketStatus.PendingCustomer, true)]
    [InlineData(TicketStatus.OnHold, true)]
    [InlineData(TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Cancelled, false)]
    public void AcceptsRepliesUnlessClosedOrCancelled(TicketStatus status, bool expected)
    {
        Assert.Equal(expected, TicketFixtures.InStatus(status).AcceptsReplies);
    }
}
