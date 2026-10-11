using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class TicketCustomerActionsTests
{
    public static TheoryData<TicketStatus, CustomerTicketAction[]> Expected() => new()
    {
        { TicketStatus.New, [CustomerTicketAction.Reply, CustomerTicketAction.RequestClosure] },
        { TicketStatus.Open, [CustomerTicketAction.Reply, CustomerTicketAction.RequestClosure] },
        { TicketStatus.PendingCustomer, [CustomerTicketAction.Reply, CustomerTicketAction.RequestClosure] },
        { TicketStatus.OnHold, [CustomerTicketAction.Reply, CustomerTicketAction.RequestClosure] },
        { TicketStatus.Resolved, [CustomerTicketAction.Reply, CustomerTicketAction.Reopen] },
        { TicketStatus.Closed, [] },
        { TicketStatus.Cancelled, [] },
    };

    [Theory]
    [MemberData(nameof(Expected))]
    public void TheCustomerMayDoWhatTheStatusAllows(TicketStatus status, CustomerTicketAction[] expected)
    {
        Assert.Equal(expected, Ticket.CustomerActionsFor(status));
    }

    [Theory]
    [MemberData(nameof(Expected))]
    public void CustomerCanAgreesWithCustomerActionsFor(TicketStatus status, CustomerTicketAction[] expected)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        foreach (CustomerTicketAction action in Enum.GetValues<CustomerTicketAction>())
        {
            Assert.Equal(expected.Contains(action), ticket.CustomerCan(action));
        }
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.PendingCustomer)]
    [InlineData(TicketStatus.OnHold)]
    public void RequestClosureResolvesAWorkingTicket(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        ticket.RequestClosure(Ids.Contact1, Clock.Later(4));

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(Clock.Later(4), ticket.ResolvedAtUtc);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(TicketActivityKind.Resolved, activity.Kind);
        Assert.Equal(Ids.Contact1, activity.ActorId);
        Assert.Equal(status.ToString(), activity.From);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void RequestClosureIsRefusedNamingTheResolveAction(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);
        int activities = ticket.Activities.Count;

        var exception = Assert.Throws<DomainException>(() => ticket.RequestClosure(Ids.Contact1, Clock.Now));

        Assert.Equal(ErrorCodes.TransitionNotAllowed, exception.Error.Code);
        Assert.Equal($"'Resolve' is not allowed while the ticket is {status}.", exception.Error.Message);
        Assert.Equal(activities, ticket.Activities.Count);
    }

    [Fact]
    public void ReopenAtCustomerRequestReopensAResolvedTicket()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Resolved);

        ticket.ReopenAtCustomerRequest(Ids.Contact1, Clock.Later(6));

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(TicketActivityKind.Reopened, activity.Kind);
        Assert.Equal(Ids.Contact1, activity.ActorId);
    }

    [Fact]
    public void ACustomerCannotReopenAClosedTicket()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Closed);

        var exception = Assert.Throws<DomainException>(() => ticket.ReopenAtCustomerRequest(Ids.Contact1, Clock.Now));

        Assert.Equal(ErrorCodes.TransitionNotAllowed, exception.Error.Code);
        Assert.Equal("'Reopen' is not allowed while the ticket is Closed.", exception.Error.Message);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.PendingCustomer)]
    [InlineData(TicketStatus.OnHold)]
    [InlineData(TicketStatus.Cancelled)]
    public void ACustomerCanOnlyReopenAResolvedTicket(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        Expect.DomainCode(() => ticket.ReopenAtCustomerRequest(Ids.Contact1, Clock.Now), ErrorCodes.TransitionNotAllowed);
    }

    [Fact]
    public void AnEmptyActorIsRefusedOnRequestClosure()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.RequestClosure(Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
        Assert.Equal(TicketStatus.Open, ticket.Status);
    }

    [Fact]
    public void AnEmptyActorIsRefusedOnReopenAtCustomerRequest()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Resolved);

        Expect.DomainCode(() => ticket.ReopenAtCustomerRequest(Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
        Assert.Equal(TicketStatus.Resolved, ticket.Status);
    }
}
