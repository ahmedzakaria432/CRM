using Crm.Modules.Customers.Contracts;
using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Application.Commands.CreateTicket;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.CreateTicket;

public sealed class CreateTicketCommandHandlerTests
{
    private static CreateTicketCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.Categories, h.UnitOfWork, h.User, h.Customers, h.Numbers, h.Assembler(), h.Clock, h.Bus);

    private static CreateTicketCommand Command(
        Guid? contact = null, string? priority = null, Guid? category = null, Guid? department = null) =>
        new(Ids.Customer1, contact, "Printer is down", "The printer shows an error.", priority, category, department);

    private static Harness ArrangedHarness(Func<Harness, Harness>? configure = null)
    {
        var h = new Harness();
        h.Quietly(() => h.Customers.GetCustomerSummaryAsync(Ids.Customer1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(TicketFixtures.Summary()))));
        return configure is null ? h : configure(h);
    }

    private static Task<Result<TicketDto>> Run(Harness h, CreateTicketCommand command) => Handler(h).Handle(command, Stubs.Ct);

    [Fact]
    public async Task AnOutOfScopeRequestedDepartmentIsForbiddenAndNothingIsDone()
    {
        Harness h = ArrangedHarness();

        Result<TicketDto> result = await Run(h, Command(department: Ids.DepartmentA2.Value));

        Expect.Failure(result, ErrorCodes.OutOfScope, ErrorType.Forbidden);
        await h.Customers.DidNotReceive().GetCustomerSummaryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await h.Numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
        Assert.Empty(h.Added);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnAmbiguousScopeWithNoDepartmentAsksForOne()
    {
        var h = new Harness(user: CurrentUsers.Agent(Scopes.AgentA1AndA2()));

        Result<TicketDto> result = await Run(h, Command());

        Expect.Failure(result, ErrorCodes.DepartmentRequired, ErrorType.Validation);
        Assert.StartsWith("DepartmentId:", result.Error.Description, StringComparison.Ordinal);
        await h.Customers.DidNotReceive().GetCustomerSummaryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await h.Numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
        Assert.Empty(h.Added);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task ARequestedDepartmentOutsideTheNarrowedScopeIsRefused()
    {
        var h = new Harness(user: CurrentUsers.Agent(Scopes.Narrowed(
            Ids.TenantA, [Scopes.Member(Ids.DepartmentA1, Ids.BranchA1), Scopes.Member(Ids.DepartmentA2, Ids.BranchA2)], Ids.DepartmentA1)));
        h.Quietly(() => h.Customers.GetCustomerSummaryAsync(Ids.Customer1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(TicketFixtures.Summary()))));

        // Narrowed to A1 only: A2 is not in the effective departments, so it is refused.
        Result<TicketDto> result = await Run(h, Command(department: Ids.DepartmentA2.Value));

        Expect.Failure(result, ErrorCodes.OutOfScope, ErrorType.Forbidden);
    }

    [Fact]
    public async Task TheBranchAndTenantComeFromTheMembershipOfTheRequestedDepartment()
    {
        var h = new Harness(user: CurrentUsers.Agent(Scopes.AgentA1AndA2()));
        h.Quietly(() => h.Customers.GetCustomerSummaryAsync(Ids.Customer1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(TicketFixtures.Summary()))));

        Result<TicketDto> result = await Run(h, Command(department: Ids.DepartmentA2.Value));

        Expect.Success(result);
        Assert.Equal(Ids.TenantA.Value, result.Value.TenantId);
        Assert.Equal(Ids.BranchA2.Value, result.Value.BranchId);
        Assert.Equal(Ids.DepartmentA2.Value, result.Value.DepartmentId);
    }

    [Theory]
    [InlineData("Authorization.Forbidden", ErrorType.Forbidden)]
    [InlineData("Customers.CustomerNotFound", ErrorType.NotFound)]
    public async Task AFailedCustomerLookupIsPassedThroughAndNoNumberIsDrawn(string code, ErrorType type)
    {
        var h = new Harness();
        h.Quietly(() => h.Customers.GetCustomerSummaryAsync(Ids.Customer1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<CustomerSummary>(new Error(code, "x", type)))));

        Result<TicketDto> result = await Run(h, Command());

        Expect.Failure(result, code, type);
        await h.Numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnInactiveCustomerIsADomainFailure()
    {
        var h = new Harness();
        h.Quietly(() => h.Customers.GetCustomerSummaryAsync(Ids.Customer1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(TicketFixtures.Summary(active: false)))));

        Result<TicketDto> result = await Run(h, Command());

        Expect.Failure(result, ErrorCodes.CustomerInactive, ErrorType.Domain);
        await h.Numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AContactThatIsNotOfTheCustomerIsADomainFailureNamingIt()
    {
        Harness h = ArrangedHarness();

        Result<TicketDto> result = await Run(h, Command(contact: Ids.Contact2));

        Expect.Failure(result, ErrorCodes.ContactNotOfCustomer, ErrorType.Domain);
        Assert.Contains(Ids.Contact2.ToString(), result.Error.Description, StringComparison.Ordinal);
        await h.Numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AContactOfTheCustomerIsAccepted()
    {
        Harness h = ArrangedHarness();

        Result<TicketDto> result = await Run(h, Command(contact: Ids.Contact1));

        Expect.Success(result);
        Assert.Equal(Ids.Contact1, result.Value.ContactId);
    }

    [Fact]
    public async Task NoContactIsAccepted()
    {
        Harness h = ArrangedHarness();

        Result<TicketDto> result = await Run(h, Command());

        Expect.Success(result);
        Assert.Null(result.Value.ContactId);
    }

    [Fact]
    public async Task AnUnknownCategoryIsNotFound()
    {
        Harness h = ArrangedHarness();

        Result<TicketDto> result = await Run(h, Command(category: Ids.Category1));

        Expect.Failure(result, ErrorCodes.CategoryNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnInactiveCategoryReachesTheAggregateAndThrows()
    {
        Category category = TicketFixtures.NewCategory(active: false);
        Harness h = ArrangedHarness().Holding(category);

        await Expect.DomainCode(() => Run(h, Command(category: category.Id.Value)), ErrorCodes.CategoryInactive);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task ACategoryOfAnotherDepartmentThrowsAfterTheNumberWasDrawn()
    {
        Category category = TicketFixtures.NewCategory(department: Ids.DepartmentA2);
        Harness h = ArrangedHarness().Holding(category);

        await Expect.DomainCode(() => Run(h, Command(category: category.Id.Value)), ErrorCodes.CategoryDepartmentMismatch);

        // The number is drawn before the aggregate runs, so a refused create leaves a gap (by design, plan 15).
        await h.Numbers.Received(1).NextAsync(Arg.Any<CancellationToken>());
        Assert.Empty(h.Added);
        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task ASuccessfulCreateAddsSavesAndReturnsTheDto()
    {
        Category category = TicketFixtures.NewCategory(ar: "تقنية", en: "Technical");
        Harness h = ArrangedHarness().Holding(category);
        h.KnowsStaff(TicketFixtures.Staff(Ids.Actor, "The Actor"));

        Result<TicketDto> result = await Run(h, Command(contact: Ids.Contact1, priority: "high", category: category.Id.Value));

        Expect.Success(result);
        Ticket added = Assert.Single(h.Added);
        Assert.Equal(TicketChannel.Agent, added.Channel);
        Assert.Equal(TicketPriority.High, added.Priority);
        Assert.Equal("T-000001", added.Number.Value);
        Assert.Equal(Clock.Now, Assert.Single(added.Activities).OccurredAtUtc);
        await h.Numbers.Received(1).NextAsync(Arg.Any<CancellationToken>());
        Assert.Equal(1, h.Saves);

        TicketDto dto = result.Value;
        Assert.Equal(added.Id.Value, dto.Id);
        Assert.Equal("T-000001", dto.Number);
        Assert.Equal("Printer is down", dto.Subject);
        Assert.Equal("The printer shows an error.", dto.Description);
        Assert.Equal("New", dto.Status);
        Assert.Equal("High", dto.Priority);
        Assert.Equal("Agent", dto.Channel);
        Assert.Equal(Ids.Customer1, dto.CustomerId);
        Assert.Equal(category.Id.Value, dto.Category?.Id);
        Assert.Equal("Technical", dto.Category?.Name.En);
        Assert.Equal("تقنية", dto.Category?.Name.Ar);
        Assert.True(dto.Category?.IsActive);
        Assert.Null(dto.AssignedAgentId);
        Assert.Null(dto.AssignedAgentName);
        Assert.Equal(Ids.TenantA.Value, dto.TenantId);
        Assert.Equal(Ids.BranchA1.Value, dto.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, dto.DepartmentId);
        Assert.Empty(dto.RowVersion);
        Assert.Equal(
            ["Open", "Resolve", "Cancel", "Assign", "Claim", "Transfer", "ChangePriority", "ChangeCategory", "Reply", "AddNote"],
            dto.AllowedActions);
        Assert.Equal(TicketActivityKind.Created.ToString(), Assert.Single(dto.Activities).Kind);
        Assert.Equal("The Actor", Assert.Single(dto.People).DisplayName);
    }

    [Theory]
    [InlineData(null, TicketPriority.Normal)]
    [InlineData("low", TicketPriority.Low)]
    [InlineData("NORMAL", TicketPriority.Normal)]
    [InlineData("high", TicketPriority.High)]
    [InlineData("URGENT", TicketPriority.Urgent)]
    public async Task ThePriorityIsParsedCaseInsensitivelyAndDefaultsToNormal(string? priority, TicketPriority expected)
    {
        Harness h = ArrangedHarness();

        Result<TicketDto> result = await Run(h, Command(priority: priority));

        Expect.Success(result);
        Assert.Equal(expected, Assert.Single(h.Added).Priority);
    }

    [Fact]
    public async Task ANumericPriorityThatBypassedTheValidatorIsAcceptedByEnumParse()
    {
        Harness h = ArrangedHarness();

        Result<TicketDto> result = await Run(h, Command(priority: "4"));

        Expect.Success(result);
        Assert.Equal(TicketPriority.Urgent, Assert.Single(h.Added).Priority);
    }

    [Fact]
    public async Task AnUnknownPriorityThatBypassedTheValidatorThrowsArgumentException()
    {
        Harness h = ArrangedHarness();

        await Assert.ThrowsAsync<ArgumentException>(() => Run(h, Command(priority: "urgentish")));

        Assert.Empty(h.Added);
    }

    [Fact]
    public async Task TheIntegrationEventCarriesEveryFieldAndIsPublishedOnceLast()
    {
        Category category = TicketFixtures.NewCategory();
        Harness h = ArrangedHarness().Holding(category);

        Result<TicketDto> result = await Run(h, Command(contact: Ids.Contact1, priority: "urgent", category: category.Id.Value));

        Expect.Success(result);
        var published = Assert.IsType<TicketCreatedIntegrationEvent>(Assert.Single(h.Published));
        Assert.NotEqual(Guid.Empty, published.Id);
        Assert.Equal(Clock.Now, published.OccurredOnUtc);
        Assert.Equal(result.Value.Id, published.TicketId);
        Assert.Equal("T-000001", published.Number);
        Assert.Equal(Ids.Customer1, published.CustomerId);
        Assert.Equal(Ids.Contact1, published.ContactId);
        Assert.Equal("Agent", published.Channel);
        Assert.Equal(category.Id.Value, published.CategoryId);
        Assert.Equal("Urgent", published.Priority);
        Assert.Equal("New", published.Status);
        Assert.Null(published.AssignedAgentId);
        Assert.Equal(Ids.Actor, published.ActorId);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
        h.AssertOrder("add", "save", "publish");
        h.AssertPublishIsLast();
        Assert.Contains("lookup", h.Log.Skip(h.Log.IndexOf("save")).Take(h.Log.IndexOf("publish") - h.Log.IndexOf("save")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AnUnidentifiableUserThrowsActorRequiredAndSavesNothing(string? userId)
    {
        var h = new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), userId));
        h.Quietly(() => h.Customers.GetCustomerSummaryAsync(Ids.Customer1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(TicketFixtures.Summary()))));

        await Expect.DomainCode(() => Run(h, Command()), ErrorCodes.ActorRequired);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task ADomainExceptionFromTheAggregateSavesAndPublishesNothing()
    {
        Harness h = ArrangedHarness();
        var command = new CreateTicketCommand(Ids.Customer1, null, "  ", "d", null, null, null);

        await Expect.DomainCode(() => Handler(h).Handle(command, Stubs.Ct), ErrorCodes.SubjectRequired);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
        Assert.Empty(h.Added);
    }
}
