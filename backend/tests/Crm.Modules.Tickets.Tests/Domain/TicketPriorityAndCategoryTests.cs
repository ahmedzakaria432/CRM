using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class TicketPriorityAndCategoryTests
{
    [Fact]
    public void ChangePriorityRecordsAnActivityAndRaisesAnEvent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        ticket.ChangePriority(TicketPriority.High, Ids.Actor, Clock.Later(2));

        Assert.Equal(TicketPriority.High, ticket.Priority);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(TicketActivityKind.PriorityChanged, activity.Kind);
        Assert.Equal("Normal", activity.From);
        Assert.Equal("High", activity.To);
        Assert.Equal(Ids.Actor, activity.ActorId);
        TicketPriorityChanged raised = Assert.IsType<TicketPriorityChanged>(Assert.Single(ticket.DomainEvents));
        Assert.Equal(TicketPriority.Normal, raised.From);
        Assert.Equal(TicketPriority.High, raised.To);
        Assert.Equal(Clock.Later(2), raised.OccurredOnUtc);
    }

    [Fact]
    public void ChangePriorityToTheSamePriorityIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.ChangePriority(TicketPriority.Normal, Ids.Actor, Clock.Now), ErrorCodes.PriorityUnchanged);
    }

    [Fact]
    public void ChangePriorityToAnUndefinedValueIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.ChangePriority((TicketPriority)9, Ids.Actor, Clock.Now), ErrorCodes.PriorityUnsupported);
        Assert.Equal(TicketPriority.Normal, ticket.Priority);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void ChangePriorityIsRefusedOnAFinishedTicket(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        Expect.DomainCode(() => ticket.ChangePriority(TicketPriority.Urgent, Ids.Actor, Clock.Now), ErrorCodes.TransitionNotAllowed);
    }

    [Fact]
    public void ChangePriorityGuardsRunActorThenStatusThenDefinedThenUnchanged()
    {
        Ticket closed = TicketFixtures.InStatus(TicketStatus.Closed);
        Expect.DomainCode(() => closed.ChangePriority((TicketPriority)9, Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
        Expect.DomainCode(() => closed.ChangePriority((TicketPriority)9, Ids.Actor, Clock.Now), ErrorCodes.TransitionNotAllowed);

        Ticket open = TicketFixtures.InStatus(TicketStatus.Open);
        Expect.DomainCode(() => open.ChangePriority((TicketPriority)9, Ids.Actor, Clock.Now), ErrorCodes.PriorityUnsupported);
        Expect.DomainCode(() => open.ChangePriority(TicketPriority.Normal, Ids.Actor, Clock.Now), ErrorCodes.PriorityUnchanged);
    }

    [Fact]
    public void ChangeCategorySetsItRecordsAnActivityAndRaisesAnEvent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Category category = TicketFixtures.NewCategory();

        ticket.ChangeCategory(category, Ids.Actor, Clock.Later(2));

        Assert.Equal(category.Id, ticket.CategoryId);
        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(TicketActivityKind.CategoryChanged, activity.Kind);
        Assert.Null(activity.From);
        Assert.Equal(category.Id.Value.ToString(), activity.To);
        TicketCategoryChanged raised = Assert.IsType<TicketCategoryChanged>(Assert.Single(ticket.DomainEvents));
        Assert.Null(raised.From);
        Assert.Equal(category.Id, raised.To);
    }

    [Fact]
    public void ChangeCategoryRecordsThePreviousCategory()
    {
        Category first = TicketFixtures.NewCategory();
        Category second = TicketFixtures.NewCategory(en: "Technical");
        Ticket ticket = TicketFixtures.Create(category: first);
        ticket.ClearDomainEvents();

        ticket.ChangeCategory(second, Ids.Actor, Clock.Later(2));

        TicketActivity activity = ticket.Activities.Last();
        Assert.Equal(first.Id.Value.ToString(), activity.From);
        Assert.Equal(second.Id.Value.ToString(), activity.To);
        TicketCategoryChanged raised = Assert.IsType<TicketCategoryChanged>(Assert.Single(ticket.DomainEvents));
        Assert.Equal(first.Id, raised.From);
    }

    [Fact]
    public void ChangeCategoryToTheCurrentOneIsRefused()
    {
        Category category = TicketFixtures.NewCategory();
        Ticket ticket = TicketFixtures.Create(category: category);

        Expect.DomainCode(() => ticket.ChangeCategory(category, Ids.Actor, Clock.Now), ErrorCodes.CategoryUnchanged);
    }

    [Fact]
    public void TheUnchangedCheckComesBeforeTheFitCheck()
    {
        Category category = TicketFixtures.NewCategory();
        Ticket ticket = TicketFixtures.Create(category: category);
        category.Deactivate();

        Expect.DomainCode(() => ticket.ChangeCategory(category, Ids.Actor, Clock.Now), ErrorCodes.CategoryUnchanged);
    }

    [Fact]
    public void ChangeCategoryToAnInactiveCategoryIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.ChangeCategory(TicketFixtures.NewCategory(active: false), Ids.Actor, Clock.Now), ErrorCodes.CategoryInactive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangeCategoryToAnotherDepartmentOrTenantIsRefused(bool otherTenant)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Category category = otherTenant
            ? TicketFixtures.NewCategory(tenant: Ids.TenantB)
            : TicketFixtures.NewCategory(department: Ids.DepartmentA2);

        Expect.DomainCode(() => ticket.ChangeCategory(category, Ids.Actor, Clock.Now), ErrorCodes.CategoryDepartmentMismatch);
    }

    [Fact]
    public void ChangeCategoryWithNullIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Action<Category, Guid, DateTimeOffset> change = ticket.ChangeCategory;

        NullArguments.AssertThrowsArgumentNull(change, null, Ids.Actor, Clock.Now);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void ChangeCategoryIsRefusedOnAFinishedTicket(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);

        Expect.DomainCode(() => ticket.ChangeCategory(TicketFixtures.NewCategory(), Ids.Actor, Clock.Now), ErrorCodes.TransitionNotAllowed);
    }

    [Fact]
    public void ChangeCategoryByAnEmptyActorIsRefused()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);

        Expect.DomainCode(() => ticket.ChangeCategory(TicketFixtures.NewCategory(), Guid.Empty, Clock.Now), ErrorCodes.ActorRequired);
    }
}
