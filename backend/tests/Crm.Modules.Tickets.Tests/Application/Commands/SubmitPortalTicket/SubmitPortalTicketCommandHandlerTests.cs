using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Tickets.Application.Commands.SubmitPortalTicket;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.SubmitPortalTicket;

public sealed class SubmitPortalTicketCommandHandlerTests
{
    private static SubmitPortalTicketCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.Categories, h.UnitOfWork, h.User, h.Numbers, h.Mapper(), h.Clock, h.Bus);

    private static SubmitPortalTicketCommand Command(Guid? category = null, string subject = "Cannot log in", string description = "The portal shows an error.") =>
        new(subject, description, category);

    private static Harness PortalHarness(PortalCustomerGrant? grant = null) =>
        new(user: CurrentUsers.Portal(grant ?? Scopes.Grant()));

    [Fact]
    public async Task ANonPortalCallerIsUnauthorizedBeforeAnyLookup()
    {
        var h = new Harness();

        Result<PortalTicketDto> result = await Handler(h).Handle(Command(), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.PortalOnly, ErrorType.Unauthorized);
        await h.Numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
        Assert.Empty(h.Added);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task ASuccessfulSubmitUsesTheGrantAndTheScopeTenant()
    {
        PortalCustomerGrant grant = Scopes.Grant(Ids.Customer2, Ids.Contact2, branch: Ids.BranchA2, department: Ids.DepartmentA2);
        Harness h = PortalHarness(grant);

        Result<PortalTicketDto> result = await Handler(h).Handle(Command(), Stubs.Ct);

        Expect.Success(result);
        Ticket added = Assert.Single(h.Added);
        Assert.Equal(TicketChannel.Portal, added.Channel);
        Assert.Equal(TicketPriority.Normal, added.Priority);
        Assert.Equal(Ids.Customer2, added.CustomerId);
        Assert.Equal(Ids.Contact2, added.ContactId);
        Assert.Equal(Ids.BranchA2, added.BranchId);
        Assert.Equal(Ids.DepartmentA2, added.DepartmentId);
        Assert.Equal(Ids.TenantA, added.TenantId);
        Assert.Equal(Ids.Contact2, Assert.Single(added.Activities).ActorId);
        Assert.Equal("T-000001", added.Number.Value);
        Assert.Equal(1, h.Saves);
        await h.Numbers.Received(1).NextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheDtoIsReceivedWithReplyAndRequestClosureAndNoCategory()
    {
        Harness h = PortalHarness();

        Result<PortalTicketDto> result = await Handler(h).Handle(Command(), Stubs.Ct);

        PortalTicketDto dto = result.Value;
        Assert.Equal("Received", dto.Status);
        Assert.Equal(["Reply", "RequestClosure"], dto.Actions);
        Assert.Null(dto.Category);
        Assert.Equal("Cannot log in", dto.Subject);
        Assert.Equal("The portal shows an error.", dto.Description);
        Assert.Equal("T-000001", dto.Number);
        Assert.Null(dto.ResolvedAtUtc);
        Assert.Null(dto.ClosedAtUtc);
    }

    [Fact]
    public async Task TheCreatedEventIsPublishedLastAfterTheMapperRead()
    {
        Category category = TicketFixtures.NewCategory();
        Harness h = PortalHarness();
        h.Quietly(() => h.Categories.Lists(category));

        Result<PortalTicketDto> result = await Handler(h).Handle(Command(category.Id.Value), Stubs.Ct);

        Expect.Success(result);
        var published = Assert.IsType<TicketCreatedIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal("Portal", published.Channel);
        Assert.Equal(Ids.Contact1, published.ActorId);
        Assert.Equal(Ids.Contact1, published.ContactId);
        Assert.Equal(Ids.Customer1, published.CustomerId);
        Assert.Equal(category.Id.Value, published.CategoryId);
        Assert.Equal("Normal", published.Priority);
        Assert.Equal("New", published.Status);
        Assert.Null(published.AssignedAgentId);
        Assert.Equal("T-000001", published.Number);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
        h.AssertOrder("add", "save", "publish");
        h.AssertPublishIsLast();
        int save = h.Log.IndexOf("save");
        Assert.Contains("lookup", h.Log.Skip(save).Take(h.Log.IndexOf("publish") - save));
    }

    [Fact]
    public async Task ACategoryIsFoundThroughTheCustomerVisibleListNeverByIdAndAppearsInTheResult()
    {
        Category category = TicketFixtures.NewCategory(ar: "تقنية", en: "Technical");
        Harness h = PortalHarness();
        h.Quietly(() => h.Categories.Lists(category));

        Result<PortalTicketDto> result = await Handler(h).Handle(Command(category.Id.Value), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(category.Id.Value, result.Value.Category?.Id);
        Assert.Equal("تقنية", result.Value.Category?.NameAr);
        Assert.Equal("Technical", result.Value.Category?.NameEn);
        await h.Categories.DidNotReceive().GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>());
        await h.Categories.Received().ListAsync(Arg.Is<Specification<Category>>(s => s is CategoriesSpecification), Arg.Any<CancellationToken>());
        Assert.Equal(category.Id, Assert.Single(h.Added).CategoryId);
    }

    public static TheoryData<string> HiddenCategories() => ["inactive", "hidden", "otherDepartment"];

    [Theory]
    [MemberData(nameof(HiddenCategories))]
    public async Task ACategoryTheCustomerMayNotPickIsNotFound(string kind)
    {
        Category category = kind switch
        {
            "inactive" => TicketFixtures.NewCategory(active: false),
            "hidden" => TicketFixtures.NewCategory(visible: false),
            _ => TicketFixtures.NewCategory(department: Ids.DepartmentA2),
        };
        Harness h = PortalHarness();
        h.Quietly(() => h.Categories.Lists(category));

        Result<PortalTicketDto> result = await Handler(h).Handle(Command(category.Id.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.CategoryNotFound, ErrorType.NotFound);
        await h.Numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
        Assert.Empty(h.Added);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnUnknownCategoryIsNotFound()
    {
        Harness h = PortalHarness();
        h.Quietly(() => h.Categories.Lists());

        Result<PortalTicketDto> result = await Handler(h).Handle(Command(Ids.Category1), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.CategoryNotFound, ErrorType.NotFound);
    }

    [Theory]
    [InlineData("", "d", ErrorCodes.SubjectRequired)]
    [InlineData("s", "  ", ErrorCodes.DescriptionRequired)]
    public async Task SubjectAndDescriptionRulesReachTheAggregate(string subject, string description, string code)
    {
        Harness h = PortalHarness();

        await Expect.DomainCode(() => Handler(h).Handle(Command(subject: subject, description: description), Stubs.Ct), code);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task ATooLongDescriptionThrowsDescriptionTooLong()
    {
        Harness h = PortalHarness();

        await Expect.DomainCode(
            () => Handler(h).Handle(Command(description: new string('d', 4001)), Stubs.Ct), ErrorCodes.DescriptionTooLong);
    }
}
