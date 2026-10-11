using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class TicketAssignmentTests
{
    [Fact]
    public void AssignToSetsTheAssigneeAndRecordsAnAssignedActivity()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        ticket.AssignTo(Ids.Agent1, Ids.Actor, Clock.Later(2));

        Assert.Equal(Ids.Agent1, ticket.AssignedAgentId);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(TicketActivityKind.Assigned, activity.Kind);
        Assert.Null(activity.From);
        Assert.Equal(Ids.Agent1.ToString(), activity.To);
        Assert.Equal(Ids.Actor, activity.ActorId);
        Assert.Equal(Clock.Later(2), activity.OccurredAtUtc);
        Assert.Equal(ticket.Revision, activity.Sequence);
    }

    [Fact]
    public void AssignToRaisesTheAssignmentChangedEvent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        ticket.AssignTo(Ids.Agent1, Ids.Actor, Clock.Later(2));

        TicketAssignmentChanged raised = Assert.IsType<TicketAssignmentChanged>(Assert.Single(ticket.DomainEvents));
        Assert.Null(raised.From);
        Assert.Equal(Ids.Agent1, raised.To);
        Assert.Equal(Clock.Later(2), raised.OccurredOnUtc);
    }

    [Fact]
    public void AssignToMayReplaceTheAssignee()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);

        ticket.AssignTo(Ids.Agent2, Ids.Actor, Clock.Later(2));

        Assert.Equal(Ids.Agent2, ticket.AssignedAgentId);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(Ids.Agent1.ToString(), activity.From);
        Assert.Equal(Ids.Agent2.ToString(), activity.To);
        TicketAssignmentChanged raised = Assert.IsType<TicketAssignmentChanged>(Assert.Single(ticket.DomainEvents));
        Assert.Equal(Ids.Agent1, raised.From);
        Assert.Equal(Ids.Agent2, raised.To);
    }

    [Fact]
    public void AssigningDoesNotChangeTheStatus()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.PendingCustomer);

        ticket.AssignTo(Ids.Agent1, Ids.Actor, Clock.Now);

        Assert.Equal(TicketStatus.PendingCustomer, ticket.Status);
    }

    [Fact]
    public void AnEmptyAgentIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.AssignTo(Guid.Empty, Ids.Actor, Clock.Now), ErrorCodes.AssigneeRequired);
    }

    [Fact]
    public void TheSameAgentIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);

        Expect.DomainCode(() => ticket.AssignTo(Ids.Agent1, Ids.Actor, Clock.Now), ErrorCodes.AssigneeUnchanged);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void AssignToIsRefusedOnAFinishedTicket(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        Expect.DomainCode(() => ticket.AssignTo(Ids.Agent1, Ids.Actor, Clock.Now), ErrorCodes.TransitionNotAllowed);
        Assert.Null(ticket.AssignedAgentId);
    }

    [Fact]
    public void AssignToGuardsRunActorThenStatusThenAgentThenUnchanged()
    {
        Ticket closed = TicketFixtures.InStatus(TicketStatus.Closed);
        Expect.DomainCode(() => closed.AssignTo(Guid.Empty, Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
        Expect.DomainCode(() => closed.AssignTo(Guid.Empty, Ids.Actor, Clock.Now), ErrorCodes.TransitionNotAllowed);

        Ticket open = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);
        Expect.DomainCode(() => open.AssignTo(Guid.Empty, Ids.Actor, Clock.Now), ErrorCodes.AssigneeRequired);
        Expect.DomainCode(() => open.AssignTo(Ids.Agent1, Ids.Actor, Clock.Now), ErrorCodes.AssigneeUnchanged);
    }

    [Fact]
    public void ClaimTakesAnUnassignedWorkingTicketForTheCaller()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.New);

        ticket.Claim(Ids.Agent1, Clock.Later(2));

        Assert.Equal(Ids.Agent1, ticket.AssignedAgentId);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(TicketActivityKind.Claimed, activity.Kind);
        Assert.Null(activity.From);
        Assert.Equal(Ids.Agent1.ToString(), activity.To);
        Assert.Equal(Ids.Agent1, activity.ActorId);
        TicketAssignmentChanged raised = Assert.IsType<TicketAssignmentChanged>(Assert.Single(ticket.DomainEvents));
        Assert.Null(raised.From);
        Assert.Equal(Ids.Agent1, raised.To);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClaimOfAnAssignedTicketIsRefusedEvenWhenAssignedToTheCaller(bool toTheCaller)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(toTheCaller ? Ids.Agent1 : Ids.Agent2);

        Expect.DomainCode(() => ticket.Claim(Ids.Agent1, Clock.Now), ErrorCodes.AlreadyAssigned);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void ClaimIsRefusedOnAFinishedTicket(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        Expect.DomainCode(() => ticket.Claim(Ids.Agent1, Clock.Now), ErrorCodes.TransitionNotAllowed);
    }

    [Fact]
    public void ClaimByAnEmptyAgentIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.Claim(Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
    }

    [Fact]
    public void UnassignClearsTheAssigneeAndRecordsAnUnassignedActivity()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);

        ticket.Unassign(Ids.Actor, Clock.Later(2));

        Assert.Null(ticket.AssignedAgentId);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(TicketActivityKind.Unassigned, activity.Kind);
        Assert.Equal(Ids.Agent1.ToString(), activity.From);
        Assert.Null(activity.To);
        TicketAssignmentChanged raised = Assert.IsType<TicketAssignmentChanged>(Assert.Single(ticket.DomainEvents));
        Assert.Equal(Ids.Agent1, raised.From);
        Assert.Null(raised.To);
    }

    [Fact]
    public void UnassignOfAnUnassignedTicketIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.Unassign(Ids.Actor, Clock.Now), ErrorCodes.NotAssigned);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void UnassignIsRefusedOnAFinishedTicket(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        Expect.DomainCode(() => ticket.Unassign(Ids.Actor, Clock.Now), ErrorCodes.TransitionNotAllowed);
    }

    [Fact]
    public void UnassignByAnEmptyActorIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);

        Expect.DomainCode(() => ticket.Unassign(Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
        Assert.Equal(Ids.Agent1, ticket.AssignedAgentId);
    }

    [Fact]
    public void ARefusedAssignmentLeavesRevisionAndEventsUntouched()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);
        int revision = ticket.Revision;

        var exception = Assert.Throws<DomainException>(() => ticket.AssignTo(Ids.Agent1, Ids.Actor, Clock.Now));

        Assert.Equal(ErrorCodes.AssigneeUnchanged, exception.Error.Code);
        Assert.Equal(revision, ticket.Revision);
        Assert.Empty(ticket.DomainEvents);
    }
}
