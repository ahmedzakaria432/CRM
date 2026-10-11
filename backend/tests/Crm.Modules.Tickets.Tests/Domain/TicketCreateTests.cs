using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class TicketCreateTests
{
    [Fact]
    public void ACreatedTicketCarriesEveryField()
    {
        Ticket ticket = Ticket.Create(
            TicketNumber.FromSequence(7), "  Printer  ", "  It is broken.  ", Ids.Customer1, Ids.Contact1, TicketChannel.Portal, null,
            TicketPriority.High, Ids.TenantA, Ids.BranchA1, Ids.DepartmentA1, Ids.Actor, Clock.Now);

        Assert.Equal("T-000007", ticket.Number.Value);
        Assert.Equal("Printer", ticket.Subject);
        Assert.Equal("It is broken.", ticket.Description);
        Assert.Equal(Ids.Customer1, ticket.CustomerId);
        Assert.Equal(Ids.Contact1, ticket.ContactId);
        Assert.Equal(TicketChannel.Portal, ticket.Channel);
        Assert.Null(ticket.CategoryId);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(Ids.TenantA, ticket.TenantId);
        Assert.Equal(Ids.BranchA1, ticket.BranchId);
        Assert.Equal(Ids.DepartmentA1, ticket.DepartmentId);
        Assert.Null(ticket.AssignedAgentId);
    }

    [Fact]
    public void ANewTicketIsNewAtRevisionOneWithOneCreatedActivity()
    {
        Ticket ticket = TicketFixtures.Create();

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal(1, ticket.Revision);
        TicketActivity activity = Assert.Single(ticket.Activities);
        Assert.Equal(TicketActivityKind.Created, activity.Kind);
        Assert.Null(activity.From);
        Assert.Equal("New", activity.To);
        Assert.Equal(1, activity.Sequence);
        Assert.Equal(Ids.Actor, activity.ActorId);
        Assert.Equal(Clock.Now, activity.OccurredAtUtc);
        Assert.NotEqual(Guid.Empty, activity.Id);
    }

    [Fact]
    public void ANewTicketHasNoConversationAndNoMilestones()
    {
        Ticket ticket = TicketFixtures.Create();

        Assert.Empty(ticket.Messages);
        Assert.Empty(ticket.Notes);
        Assert.Null(ticket.FirstRespondedAtUtc);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
        Assert.Empty(ticket.RowVersion);
        Assert.False(ticket.IsDeleted);
        Assert.Null(ticket.DeletedAtUtc);
        Assert.Null(ticket.DeletedBy);
    }

    [Fact]
    public void ASuppliedIdWins()
    {
        var id = new TicketId(Ids.Ticket1);

        Assert.Equal(id, TicketFixtures.Create(id: id).Id);
    }

    [Fact]
    public void AnIdIsGeneratedOtherwise()
    {
        Ticket first = TicketFixtures.Create();
        Ticket second = TicketFixtures.Create();

        Assert.NotEqual(Guid.Empty, first.Id.Value);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void CreatingRaisesOneTicketCreatedEvent()
    {
        Ticket ticket = TicketFixtures.Create(at: Clock.Later(5));

        TicketCreated raised = Assert.IsType<TicketCreated>(Assert.Single(ticket.DomainEvents));
        Assert.Equal(ticket.Id, raised.TicketId);
        Assert.Equal(Clock.Later(5), raised.OccurredOnUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnEmptyContactIdBecomesNull(bool empty)
    {
        Ticket ticket = TicketFixtures.Create(contact: empty ? Guid.Empty : null);

        Assert.Null(ticket.ContactId);
    }

    [Fact]
    public void ANullArgumentIsRefused()
    {
        Func<TicketNumber, string, string, Guid, Guid?, TicketChannel, Category?, TicketPriority, TenantId, BranchId, DepartmentId, Guid, DateTimeOffset, TicketId?, Ticket> create = Ticket.Create;
        TicketNumber number = TicketNumber.FromSequence(1);

        NullArguments.AssertThrowsArgumentNull(create, null, "s", "d", Ids.Customer1, null, TicketChannel.Agent, null, TicketPriority.Normal, Ids.TenantA, Ids.BranchA1, Ids.DepartmentA1, Ids.Actor, Clock.Now, null);
        NullArguments.AssertThrowsArgumentNull(create, number, "s", "d", Ids.Customer1, null, TicketChannel.Agent, null, TicketPriority.Normal, null, Ids.BranchA1, Ids.DepartmentA1, Ids.Actor, Clock.Now, null);
        NullArguments.AssertThrowsArgumentNull(create, number, "s", "d", Ids.Customer1, null, TicketChannel.Agent, null, TicketPriority.Normal, Ids.TenantA, null, Ids.DepartmentA1, Ids.Actor, Clock.Now, null);
        NullArguments.AssertThrowsArgumentNull(create, number, "s", "d", Ids.Customer1, null, TicketChannel.Agent, null, TicketPriority.Normal, Ids.TenantA, Ids.BranchA1, null, Ids.Actor, Clock.Now, null);
        NullArguments.AssertThrowsArgumentNull(create, number, null, "d", Ids.Customer1, null, TicketChannel.Agent, null, TicketPriority.Normal, Ids.TenantA, Ids.BranchA1, Ids.DepartmentA1, Ids.Actor, Clock.Now, null);
        NullArguments.AssertThrowsArgumentNull(create, number, "s", null, Ids.Customer1, null, TicketChannel.Agent, null, TicketPriority.Normal, Ids.TenantA, Ids.BranchA1, Ids.DepartmentA1, Ids.Actor, Clock.Now, null);
    }

    [Fact]
    public void ANoneTenantIsRefused()
    {
        Expect.DomainCode(() => TicketFixtures.Create(tenant: TenantId.None), ErrorCodes.ScopeRequired);
    }

    [Fact]
    public void AnEmptyCustomerIsRefused()
    {
        Expect.DomainCode(() => TicketFixtures.Create(customer: Guid.Empty), ErrorCodes.CustomerRequired);
    }

    [Fact]
    public void AnEmptyActorIsRefused()
    {
        Expect.DomainCode(() => TicketFixtures.Create(actor: Guid.Empty), ErrorCodes.ActorRequired);
    }

    [Fact]
    public void TheGuardsRunInTheDocumentedOrder()
    {
        // Scope first, then customer, actor, subject, description, channel, priority, category.
        Expect.DomainCode(
            () => Ticket.Create(
                TicketNumber.FromSequence(1), "", "", Guid.Empty, null, (TicketChannel)9, null, (TicketPriority)9, TenantId.None,
                Ids.BranchA1, Ids.DepartmentA1, Guid.Empty, Clock.Now),
            ErrorCodes.ScopeRequired);
        Expect.DomainCode(
            () => Ticket.Create(
                TicketNumber.FromSequence(1), "", "", Guid.Empty, null, (TicketChannel)9, null, (TicketPriority)9, Ids.TenantA,
                Ids.BranchA1, Ids.DepartmentA1, Guid.Empty, Clock.Now),
            ErrorCodes.CustomerRequired);
        Expect.DomainCode(
            () => Ticket.Create(
                TicketNumber.FromSequence(1), "", "", Ids.Customer1, null, (TicketChannel)9, null, (TicketPriority)9, Ids.TenantA,
                Ids.BranchA1, Ids.DepartmentA1, Guid.Empty, Clock.Now),
            ErrorCodes.ActorRequired);
        Expect.DomainCode(
            () => Ticket.Create(
                TicketNumber.FromSequence(1), "", "", Ids.Customer1, null, (TicketChannel)9, null, (TicketPriority)9, Ids.TenantA,
                Ids.BranchA1, Ids.DepartmentA1, Ids.Actor, Clock.Now),
            ErrorCodes.SubjectRequired);
        Expect.DomainCode(
            () => Ticket.Create(
                TicketNumber.FromSequence(1), "s", "", Ids.Customer1, null, (TicketChannel)9, null, (TicketPriority)9, Ids.TenantA,
                Ids.BranchA1, Ids.DepartmentA1, Ids.Actor, Clock.Now),
            ErrorCodes.DescriptionRequired);
        Expect.DomainCode(
            () => Ticket.Create(
                TicketNumber.FromSequence(1), "s", "d", Ids.Customer1, null, (TicketChannel)9, null, (TicketPriority)9, Ids.TenantA,
                Ids.BranchA1, Ids.DepartmentA1, Ids.Actor, Clock.Now),
            ErrorCodes.ChannelUnsupported);
        Expect.DomainCode(
            () => Ticket.Create(
                TicketNumber.FromSequence(1), "s", "d", Ids.Customer1, null, TicketChannel.Agent, TicketFixtures.NewCategory(active: false),
                (TicketPriority)9, Ids.TenantA, Ids.BranchA1, Ids.DepartmentA1, Ids.Actor, Clock.Now),
            ErrorCodes.PriorityUnsupported);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankSubjectIsRefused(string subject)
    {
        Expect.DomainCode(() => TicketFixtures.Create(subject: subject), ErrorCodes.SubjectRequired);
    }

    [Fact]
    public void ASubjectOfTwoHundredCharactersIsAccepted()
    {
        Assert.Equal(200, TicketFixtures.Create(subject: new string('s', 200)).Subject.Length);
    }

    [Fact]
    public void ASubjectOfTwoHundredAndOneIsRefused()
    {
        Expect.DomainCode(() => TicketFixtures.Create(subject: new string('s', 201)), ErrorCodes.SubjectTooLong);
    }

    [Fact]
    public void TheSubjectLengthIsCheckedAfterTheTrim()
    {
        Assert.Equal(200, TicketFixtures.Create(subject: " " + new string('s', 200) + " ").Subject.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankDescriptionIsRefused(string description)
    {
        Expect.DomainCode(() => TicketFixtures.Create(description: description), ErrorCodes.DescriptionRequired);
    }

    [Fact]
    public void ADescriptionOfFourThousandCharactersIsAccepted()
    {
        Assert.Equal(4000, TicketFixtures.Create(description: new string('d', 4000)).Description.Length);
    }

    [Fact]
    public void ADescriptionOfFourThousandAndOneIsRefused()
    {
        Expect.DomainCode(() => TicketFixtures.Create(description: new string('d', 4001)), ErrorCodes.DescriptionTooLong);
    }

    [Fact]
    public void TheDescriptionLengthIsCheckedAfterTheTrim()
    {
        Assert.Equal(4000, TicketFixtures.Create(description: " " + new string('d', 4000) + " ").Description.Length);
    }

    [Fact]
    public void AnArabicSubjectAndDescriptionAreKept()
    {
        Ticket ticket = TicketFixtures.Create(subject: "لا أستطيع تسجيل الدخول", description: "يظهر لي خطأ");

        Assert.Equal("لا أستطيع تسجيل الدخول", ticket.Subject);
        Assert.Equal("يظهر لي خطأ", ticket.Description);
    }

    [Fact]
    public void AnUndefinedChannelIsRefused()
    {
        Expect.DomainCode(() => TicketFixtures.Create(channel: (TicketChannel)9), ErrorCodes.ChannelUnsupported);
    }

    [Fact]
    public void AnUndefinedPriorityIsRefused()
    {
        Expect.DomainCode(() => TicketFixtures.Create(priority: (TicketPriority)9), ErrorCodes.PriorityUnsupported);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Normal)]
    [InlineData(TicketPriority.High)]
    [InlineData(TicketPriority.Urgent)]
    public void EveryDefinedPriorityIsAccepted(TicketPriority priority)
    {
        Assert.Equal(priority, TicketFixtures.Create(priority: priority).Priority);
    }

    [Fact]
    public void AnActiveCategoryOfTheSameDepartmentIsSetAndNotMutated()
    {
        Category category = TicketFixtures.NewCategory();
        string before = category.Name.English;

        Ticket ticket = TicketFixtures.Create(category: category);

        Assert.Equal(category.Id, ticket.CategoryId);
        Assert.Equal(before, category.Name.English);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void AnInactiveCategoryIsRefused()
    {
        Category category = TicketFixtures.NewCategory(active: false);

        Expect.DomainCode(() => TicketFixtures.Create(category: category), ErrorCodes.CategoryInactive);
    }

    [Fact]
    public void ACategoryOfAnotherDepartmentIsRefused()
    {
        Category category = TicketFixtures.NewCategory(department: Ids.DepartmentA2);

        Expect.DomainCode(() => TicketFixtures.Create(category: category), ErrorCodes.CategoryDepartmentMismatch);
    }

    [Fact]
    public void ACategoryOfAnotherTenantIsRefused()
    {
        Category category = TicketFixtures.NewCategory(tenant: Ids.TenantB);

        Expect.DomainCode(() => TicketFixtures.Create(category: category), ErrorCodes.CategoryDepartmentMismatch);
    }

    [Fact]
    public void AnInactiveCategoryOfAnotherDepartmentReportsInactiveFirst()
    {
        Category category = TicketFixtures.NewCategory(department: Ids.DepartmentA2, active: false);

        Expect.DomainCode(() => TicketFixtures.Create(category: category), ErrorCodes.CategoryInactive);
    }

    [Fact]
    public void ATicketIsScopedAndPortalOwned()
    {
        Ticket ticket = TicketFixtures.Create();

        Assert.IsAssignableFrom<ITransferableOrganisationScoped>(ticket);
        Assert.IsAssignableFrom<IPortalCustomerOwned>(ticket);
        Assert.IsAssignableFrom<ISoftDeletable>(ticket);
        Assert.IsAssignableFrom<IHasRowVersion>(ticket);
        Assert.Equal(Ids.Customer1, ((IPortalCustomerOwned)ticket).CustomerId);
    }
}
