using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class TicketEventsTests
{
    private static readonly TicketId Id = new(Ids.Ticket1);

    [Fact]
    public void TicketCreatedCarriesItsValues()
    {
        var created = new TicketCreated(Id, Clock.Now);

        Assert.Equal(Id, created.TicketId);
        Assert.Equal(Clock.Now, created.OccurredOnUtc);
        Assert.Equal(new TicketCreated(Id, Clock.Now), created);
        Assert.NotEqual(new TicketCreated(Id, Clock.Later(1)), created);
        Assert.IsAssignableFrom<IDomainEvent>(created);
    }

    [Fact]
    public void TicketStatusChangedCarriesItsValues()
    {
        var changed = new TicketStatusChanged(Id, TicketStatus.New, TicketStatus.Open, Clock.Now);

        Assert.Equal(TicketStatus.New, changed.From);
        Assert.Equal(TicketStatus.Open, changed.To);
        Assert.Equal(new TicketStatusChanged(Id, TicketStatus.New, TicketStatus.Open, Clock.Now), changed);
        Assert.NotEqual(new TicketStatusChanged(Id, TicketStatus.New, TicketStatus.OnHold, Clock.Now), changed);
    }

    [Fact]
    public void TicketAssignmentChangedCarriesItsValues()
    {
        var changed = new TicketAssignmentChanged(Id, null, Ids.Agent1, Clock.Now);

        Assert.Null(changed.From);
        Assert.Equal(Ids.Agent1, changed.To);
        Assert.Equal(new TicketAssignmentChanged(Id, null, Ids.Agent1, Clock.Now), changed);
    }

    [Fact]
    public void TicketTransferredCarriesItsValues()
    {
        var transferred = new TicketTransferred(Id, Ids.DepartmentA1, Ids.DepartmentA2, Clock.Now);

        Assert.Equal(Ids.DepartmentA1, transferred.From);
        Assert.Equal(Ids.DepartmentA2, transferred.To);
        Assert.Equal(new TicketTransferred(Id, Ids.DepartmentA1, Ids.DepartmentA2, Clock.Now), transferred);
    }

    [Fact]
    public void TicketPriorityChangedCarriesItsValues()
    {
        var changed = new TicketPriorityChanged(Id, TicketPriority.Low, TicketPriority.Urgent, Clock.Now);

        Assert.Equal(TicketPriority.Low, changed.From);
        Assert.Equal(TicketPriority.Urgent, changed.To);
        Assert.Equal(new TicketPriorityChanged(Id, TicketPriority.Low, TicketPriority.Urgent, Clock.Now), changed);
    }

    [Fact]
    public void TicketCategoryChangedCarriesItsValues()
    {
        var first = new CategoryId(Ids.Category1);
        var second = new CategoryId(Ids.Category2);
        var changed = new TicketCategoryChanged(Id, first, second, Clock.Now);

        Assert.Equal(first, changed.From);
        Assert.Equal(second, changed.To);
        Assert.Equal(new TicketCategoryChanged(Id, first, second, Clock.Now), changed);
        Assert.Null(new TicketCategoryChanged(Id, null, second, Clock.Now).From);
    }

    [Fact]
    public void TicketMessageAddedCarriesItsValues()
    {
        var added = new TicketMessageAdded(Id, Ids.Missing, TicketMessageDirection.Inbound, Clock.Now);

        Assert.Equal(Ids.Missing, added.MessageId);
        Assert.Equal(TicketMessageDirection.Inbound, added.Direction);
        Assert.Equal(new TicketMessageAdded(Id, Ids.Missing, TicketMessageDirection.Inbound, Clock.Now), added);
    }

    [Fact]
    public void TicketNoteAddedCarriesItsValues()
    {
        var added = new TicketNoteAdded(Id, Ids.Missing, Clock.Now);

        Assert.Equal(Ids.Missing, added.NoteId);
        Assert.Equal(new TicketNoteAdded(Id, Ids.Missing, Clock.Now), added);
    }

    public static TheoryData<string, Func<Ticket>, Type[]> MethodsAndTheirEvents() => new()
    {
        { "Open", () => Run(TicketStatus.New, t => t.Open(Ids.Agent1, Clock.Later(1))), [typeof(TicketStatusChanged)] },
        { "AwaitCustomer", () => Run(TicketStatus.Open, t => t.AwaitCustomer(Ids.Agent1, Clock.Later(1))), [typeof(TicketStatusChanged)] },
        { "PutOnHold", () => Run(TicketStatus.Open, t => t.PutOnHold(Ids.Agent1, Clock.Later(1))), [typeof(TicketStatusChanged)] },
        { "Resolve", () => Run(TicketStatus.Open, t => t.Resolve(Ids.Agent1, Clock.Later(1))), [typeof(TicketStatusChanged)] },
        { "Close", () => Run(TicketStatus.Resolved, t => t.Close(Ids.Agent1, Clock.Later(1))), [typeof(TicketStatusChanged)] },
        { "Reopen", () => Run(TicketStatus.Closed, t => t.Reopen(Ids.Agent1, Clock.Later(1))), [typeof(TicketStatusChanged)] },
        { "Cancel", () => Run(TicketStatus.New, t => t.Cancel(Ids.Agent1, Clock.Later(1))), [typeof(TicketStatusChanged)] },
        { "AssignTo", () => Run(TicketStatus.Open, t => t.AssignTo(Ids.Agent1, Ids.Actor, Clock.Later(1))), [typeof(TicketAssignmentChanged)] },
        { "Claim", () => Run(TicketStatus.Open, t => t.Claim(Ids.Agent1, Clock.Later(1))), [typeof(TicketAssignmentChanged)] },
        { "Unassign", () => Run(TicketStatus.Open, t => { t.AssignTo(Ids.Agent1, Ids.Actor, Clock.Later(1)); t.ClearDomainEvents(); t.Unassign(Ids.Actor, Clock.Later(2)); }), [typeof(TicketAssignmentChanged)] },
        { "TransferTo", () => Run(TicketStatus.Open, t => t.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Later(1))), [typeof(TicketTransferred)] },
        { "ChangePriority", () => Run(TicketStatus.Open, t => t.ChangePriority(TicketPriority.High, Ids.Actor, Clock.Later(1))), [typeof(TicketPriorityChanged)] },
        { "ChangeCategory", () => Run(TicketStatus.Open, t => t.ChangeCategory(TicketFixtures.NewCategory(), Ids.Actor, Clock.Later(1))), [typeof(TicketCategoryChanged)] },
        { "ReplyAsAgent", () => Run(TicketStatus.Open, t => t.ReplyAsAgent("x", Ids.Agent1, Clock.Later(1))), [typeof(TicketMessageAdded)] },
        { "ReplyAsAgentOnResolved", () => Run(TicketStatus.Resolved, t => t.ReplyAsAgent("x", Ids.Agent1, Clock.Later(1))), [typeof(TicketMessageAdded), typeof(TicketStatusChanged)] },
        { "ReceiveCustomerReply", () => Run(TicketStatus.Open, t => t.ReceiveCustomerReply("x", Ids.Contact1, Ids.Agent1, Clock.Later(1))), [typeof(TicketMessageAdded)] },
        { "ReceiveCustomerReplyOnPending", () => Run(TicketStatus.PendingCustomer, t => t.ReceiveCustomerReply("x", Ids.Contact1, Ids.Agent1, Clock.Later(1))), [typeof(TicketMessageAdded), typeof(TicketStatusChanged)] },
        { "AddInternalNote", () => Run(TicketStatus.Closed, t => t.AddInternalNote("x", Ids.Agent1, Clock.Later(1))), [typeof(TicketNoteAdded)] },
        { "RequestClosure", () => Run(TicketStatus.Open, t => t.RequestClosure(Ids.Contact1, Clock.Later(1))), [typeof(TicketStatusChanged)] },
        { "ReopenAtCustomerRequest", () => Run(TicketStatus.Resolved, t => t.ReopenAtCustomerRequest(Ids.Contact1, Clock.Later(1))), [typeof(TicketStatusChanged)] },
    };

    private static Ticket Run(TicketStatus start, Action<Ticket> act)
    {
        Ticket ticket = TicketFixtures.InStatus(start);
        act(ticket);
        return ticket;
    }

    [Theory]
    [MemberData(nameof(MethodsAndTheirEvents))]
    public void EveryMethodRaisesExactlyTheListedEventsInOrder(string method, Func<Ticket> arrange, Type[] expected)
    {
        Ticket ticket = arrange();

        Assert.Equal(expected, ticket.DomainEvents.Select(e => e.GetType()));
        Assert.True(ticket.DomainEvents.All(e => e.OccurredOnUtc >= Clock.Later(1)), method);
    }

    [Fact]
    public void ClearingDomainEventsEmptiesTheList()
    {
        Ticket ticket = TicketFixtures.Create();
        Assert.NotEmpty(ticket.DomainEvents);

        ticket.ClearDomainEvents();

        Assert.Empty(ticket.DomainEvents);
    }
}
