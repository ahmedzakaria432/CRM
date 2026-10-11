using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Tests.Domain;

/// <summary>TicketDomainErrors is internal, so each of the 28 codes is provoked through the aggregate and compared with the literal.</summary>
public sealed class DomainErrorCodesTests
{
    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Error.Code;

    private static Ticket Working() => TicketFixtures.InStatus(TicketStatus.Open);

    private static IEnumerable<(string Code, Action Provoke)> Provocations()
    {
        yield return (ErrorCodes.ScopeRequired, () => TicketFixtures.Create(tenant: TenantIdNone()));
        yield return (ErrorCodes.NumberInvalid, () => TicketNumber.FromSequence(0));
        yield return (ErrorCodes.SubjectRequired, () => TicketFixtures.Create(subject: " "));
        yield return (ErrorCodes.SubjectTooLong, () => TicketFixtures.Create(subject: new string('s', 201)));
        yield return (ErrorCodes.DescriptionRequired, () => TicketFixtures.Create(description: " "));
        yield return (ErrorCodes.DescriptionTooLong, () => TicketFixtures.Create(description: new string('d', 4001)));
        yield return (ErrorCodes.CustomerRequired, () => TicketFixtures.Create(customer: Guid.Empty));
        yield return (ErrorCodes.ActorRequired, () => TicketFixtures.Create(actor: Guid.Empty));
        yield return (ErrorCodes.ChannelUnsupported, () => TicketFixtures.Create(channel: (TicketChannel)9));
        yield return (ErrorCodes.PriorityUnsupported, () => TicketFixtures.Create(priority: (TicketPriority)9));
        yield return (ErrorCodes.PriorityUnchanged, () => Working().ChangePriority(TicketPriority.Normal, Ids.Actor, Clock.Now));
        yield return (ErrorCodes.AlreadyAssigned, () => Working().Assigned(Ids.Agent1).Claim(Ids.Agent2, Clock.Now));
        yield return (ErrorCodes.AssigneeRequired, () => Working().AssignTo(Guid.Empty, Ids.Actor, Clock.Now));
        yield return (ErrorCodes.AssigneeUnchanged, () => Working().Assigned(Ids.Agent1).AssignTo(Ids.Agent1, Ids.Actor, Clock.Now));
        yield return (ErrorCodes.NotAssigned, () => Working().Unassign(Ids.Actor, Clock.Now));
        yield return (ErrorCodes.TransferToSameDepartment, () => Working().TransferTo(Ids.DepartmentA1, Ids.BranchA1, Ids.Actor, Clock.Now));
        yield return (ErrorCodes.CategoryInactive, () => Working().ChangeCategory(TicketFixtures.NewCategory(active: false), Ids.Actor, Clock.Now));
        yield return (ErrorCodes.CategoryDepartmentMismatch, () => Working().ChangeCategory(TicketFixtures.NewCategory(department: Ids.DepartmentA2), Ids.Actor, Clock.Now));
        yield return (ErrorCodes.CategoryUnchanged, () =>
        {
            Category category = TicketFixtures.NewCategory();
            TicketFixtures.Create(category: category).ChangeCategory(category, Ids.Actor, Clock.Now);
        });
        yield return (ErrorCodes.CategoryNameRequired, () => LocalizedText.Create("", "x"));
        yield return (ErrorCodes.CategoryNameTooLong, () => LocalizedText.Create(new string('x', 201), "x"));
        yield return (ErrorCodes.CategoryDisplayOrderInvalid, () => TicketFixtures.NewCategory(order: -1));
        yield return (ErrorCodes.CategoryAlreadyActive, () => TicketFixtures.NewCategory().Reactivate());
        yield return (ErrorCodes.CategoryAlreadyInactive, () => TicketFixtures.NewCategory(active: false).Deactivate());
        yield return (ErrorCodes.CategoryScopeRequired, () => TicketFixtures.NewCategory(tenant: TenantIdNone()));
        yield return (ErrorCodes.BodyRequired, () => Working().AddInternalNote(" ", Ids.Agent1, Clock.Now));
        yield return (ErrorCodes.BodyTooLong, () => Working().AddInternalNote(new string('n', 10_001), Ids.Agent1, Clock.Now));
        yield return (ErrorCodes.TransitionNotAllowed, () => TicketFixtures.InStatus(TicketStatus.Closed).Cancel(Ids.Actor, Clock.Now));
    }

    private static Crm.SharedKernel.Scoping.TenantId TenantIdNone() => Crm.SharedKernel.Scoping.TenantId.None;

    [Fact]
    public void TheCatalogueHasTwentyEightCodes()
    {
        Assert.Equal(28, ErrorCodes.Domain.Count);
        Assert.Equal(28, Provocations().Count());
    }

    [Fact]
    public void EveryCodeIsUniqueAndPrefixed()
    {
        Assert.Equal(ErrorCodes.Domain.Count, ErrorCodes.Domain.Distinct().Count());
        Assert.All(ErrorCodes.Domain, code => Assert.StartsWith("Tickets.", code, StringComparison.Ordinal));
    }

    [Fact]
    public void EveryCodeIsRaisedByTheAggregateExactlyAsTheContractSays()
    {
        List<string> raised = [.. Provocations().Select(p => CodeOf(p.Provoke))];

        Assert.Equal(Provocations().Select(p => p.Code), raised);
        Assert.Equal(ErrorCodes.Domain.Order(StringComparer.Ordinal), raised.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryErrorHasAReadableMessage()
    {
        foreach ((string code, Action provoke) in Provocations())
        {
            var exception = Assert.Throws<DomainException>(provoke);

            Assert.False(string.IsNullOrWhiteSpace(exception.Error.Message), code);
            Assert.Equal(exception.Error.Message, exception.Message);
        }
    }

    [Fact]
    public void TheTransitionMessageNamesTheActionAndTheStatus()
    {
        var exception = Assert.Throws<DomainException>(() => TicketFixtures.InStatus(TicketStatus.Closed).Cancel(Ids.Actor, Clock.Now));

        Assert.Equal("'Cancel' is not allowed while the ticket is Closed.", exception.Error.Message);
    }
}
