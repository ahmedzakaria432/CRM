using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class TicketTransferTests
{
    private static Ticket Categorised()
    {
        Ticket ticket = TicketFixtures.Create(category: TicketFixtures.NewCategory(), contact: Ids.Contact1);
        ticket.ClearDomainEvents();
        return ticket;
    }

    [Fact]
    public void TransferMovesDepartmentAndBranch()
    {
        Ticket ticket = Categorised();

        ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Later(2));

        Assert.Equal(Ids.DepartmentA2, ticket.DepartmentId);
        Assert.Equal(Ids.BranchA2, ticket.BranchId);
    }

    [Fact]
    public void TransferClearsTheAssigneeAndTheCategory()
    {
        Ticket ticket = Categorised().Assigned(Ids.Agent1);

        ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Later(2));

        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(ticket.CategoryId);
    }

    [Fact]
    public void TransferKeepsEveryEarlierActivityAndAppendsOne()
    {
        Ticket ticket = Categorised().Assigned(Ids.Agent1);
        List<Guid> before = [.. ticket.Activities.Select(a => a.Id)];

        ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Later(2));

        Assert.Equal(before.Count + 1, ticket.Activities.Count);
        Assert.Equal(before, ticket.Activities.Take(before.Count).Select(a => a.Id));
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(TicketActivityKind.Transferred, activity.Kind);
        Assert.Equal(Ids.DepartmentA1.Value.ToString(), activity.From);
        Assert.Equal(Ids.DepartmentA2.Value.ToString(), activity.To);
        Assert.Equal(Ids.Actor, activity.ActorId);
        Assert.Equal(ticket.Revision, activity.Sequence);
    }

    [Fact]
    public void TransferRaisesTheTransferredEvent()
    {
        Ticket ticket = Categorised();

        ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Later(2));

        TicketTransferred raised = Assert.IsType<TicketTransferred>(Assert.Single(ticket.DomainEvents));
        Assert.Equal(Ids.DepartmentA1, raised.From);
        Assert.Equal(Ids.DepartmentA2, raised.To);
        Assert.Equal(Clock.Later(2), raised.OccurredOnUtc);
    }

    [Fact]
    public void TransferToTheSameDepartmentIsRefused()
    {
        Ticket ticket = Categorised();

        Expect.DomainCode(() => ticket.TransferTo(Ids.DepartmentA1, Ids.BranchA1, Ids.Actor, Clock.Now), ErrorCodes.TransferToSameDepartment);
        Assert.NotNull(ticket.CategoryId);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void TransferOfAFinishedTicketIsRefusedBeforeTheSameDepartmentCheck(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        Expect.DomainCode(() => ticket.TransferTo(Ids.DepartmentA1, Ids.BranchA1, Ids.Actor, Clock.Now), ErrorCodes.TransitionNotAllowed);
        Expect.DomainCode(() => ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Now), ErrorCodes.TransitionNotAllowed);
    }

    [Fact]
    public void ANullDepartmentOrBranchIsRefused()
    {
        Ticket ticket = Categorised();
        Action<DepartmentId, BranchId, Guid, DateTimeOffset> transfer = ticket.TransferTo;

        NullArguments.AssertThrowsArgumentNull(transfer, null, Ids.BranchA2, Ids.Actor, Clock.Now);
        NullArguments.AssertThrowsArgumentNull(transfer, Ids.DepartmentA2, null, Ids.Actor, Clock.Now);
    }

    [Fact]
    public void AnEmptyActorIsRefused()
    {
        Ticket ticket = Categorised();

        Expect.DomainCode(() => ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
        Assert.Equal(Ids.DepartmentA1, ticket.DepartmentId);
    }

    [Fact]
    public void TheTenantCustomerContactChannelAndNumberNeverChange()
    {
        Ticket ticket = Categorised();

        ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Later(2));

        Assert.Equal(Ids.TenantA, ticket.TenantId);
        Assert.Equal(Ids.Customer1, ticket.CustomerId);
        Assert.Equal(Ids.Contact1, ticket.ContactId);
        Assert.Equal(TicketChannel.Agent, ticket.Channel);
        Assert.Equal("T-000001", ticket.Number.Value);
    }

    [Fact]
    public void TheTicketIsTransferableAndPortalOwnedWithAnUnchangedCustomer()
    {
        Ticket ticket = Categorised();

        ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Later(2));

        Assert.IsAssignableFrom<ITransferableOrganisationScoped>(ticket);
        IPortalCustomerOwned owned = Assert.IsAssignableFrom<IPortalCustomerOwned>(ticket);
        Assert.Equal(Ids.Customer1, owned.CustomerId);
        Assert.Equal(Ids.DepartmentA2, ((IOrganisationScoped)ticket).DepartmentId);
    }

    [Fact]
    public void AfterATransferACategoryOfTheNewDepartmentCanBeSet()
    {
        Ticket ticket = Categorised();
        ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Later(2));
        Category received = TicketFixtures.NewCategory(department: Ids.DepartmentA2);

        ticket.ChangeCategory(received, Ids.Actor, Clock.Later(3));

        Assert.Equal(received.Id, ticket.CategoryId);
    }

    [Fact]
    public void AfterATransferTheOldDepartmentsCategoryNoLongerFits()
    {
        Category original = TicketFixtures.NewCategory();
        Ticket ticket = TicketFixtures.Create(category: original);
        ticket.TransferTo(Ids.DepartmentA2, Ids.BranchA2, Ids.Actor, Clock.Later(2));

        Expect.DomainCode(() => ticket.ChangeCategory(original, Ids.Actor, Clock.Later(3)), ErrorCodes.CategoryDepartmentMismatch);
    }
}
