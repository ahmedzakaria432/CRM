using Crm.Modules.Tickets.Application.Commands.TransferTicket;
using Crm.Modules.Tickets.Contracts.IntegrationEvents;
using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Commands.TransferTicket;

public sealed class TransferTicketCommandHandlerTests
{
    private static TransferTicketCommandHandler Handler(Harness h) =>
        new(h.Tickets, h.UnitOfWork, h.User, h.Organisation, h.Assembler(), h.Localization, h.Clock, h.Bus);

    private static TransferTicketCommand Command(Ticket ticket, Guid department) => new(ticket.Id.Value, department, Ids.RowVersion());

    private static Harness HarnessFor(Ticket ticket, bool targetActive = true)
    {
        var h = new Harness { Localization = FakeLocalization.Resources() };
        h.Holding(ticket);
        h.KnowsDepartments(TicketFixtures.Department(Ids.DepartmentA2, branch: Ids.BranchA2, code: "A2", active: targetActive));
        return h;
    }

    [Fact]
    public async Task AnUnknownTicketIsNotFound()
    {
        var h = new Harness();

        Result<TicketDto> result = await Handler(h).Handle(new TransferTicketCommand(Ids.Ticket1, Ids.DepartmentA2.Value, Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TicketNotFound, ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task TheRowVersionIsSetBeforeTheChangeAndPublishIsLast()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket);

        await Handler(h).Handle(Command(ticket, Ids.DepartmentA2.Value), Stubs.Ct);

        h.Tickets.Received(1).SetOriginalRowVersion(ticket, Arg.Is<byte[]>(v => v.SequenceEqual(Ids.RowVersion())));
        h.AssertOrder("rowversion", "save", "publish");
        h.AssertPublishIsLast();
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task AFinishedTicketIsRefusedBeforeTheDepartmentIsRead(TicketStatus status)
    {
        Ticket ticket = TicketFixtures.InStatus(status);
        Harness h = HarnessFor(ticket);

        using UiCulture culture = UiCulture.Use("en");
        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.DepartmentA2.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TransitionNotAllowed, ErrorType.Domain);
        Assert.Contains(status.ToString(), result.Error.Description, StringComparison.Ordinal);
        await h.Organisation.DidNotReceive().GetDepartmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, h.Saves);
    }

    [Fact]
    public async Task AFailedDepartmentLookupIsPassedThrough()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        var h = new Harness();
        h.Holding(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.DepartmentA2.Value), Stubs.Ct);

        Expect.Failure(result, "Organisation.DepartmentNotFound", ErrorType.NotFound);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task AnInactiveTargetDepartmentIsADomainFailure()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        Harness h = HarnessFor(ticket, targetActive: false);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.DepartmentA2.Value), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.TargetDepartmentInactive, ErrorType.Domain);
        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task TheSameDepartmentThrowsTransferToSameDepartment()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        var h = new Harness();
        h.Holding(ticket);
        h.KnowsDepartments(TicketFixtures.Department(Ids.DepartmentA1));

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, Ids.DepartmentA1.Value), Stubs.Ct), ErrorCodes.TransferToSameDepartment);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }

    [Fact]
    public async Task ATransferMovesDepartmentAndBranchAndClearsAssigneeAndCategory()
    {
        Ticket ticket = TicketFixtures.Create(category: TicketFixtures.NewCategory());
        ticket.AssignTo(Ids.Agent1, Ids.Actor, Clock.Now);
        Harness h = HarnessFor(ticket);

        Result<TicketDto> result = await Handler(h).Handle(Command(ticket, Ids.DepartmentA2.Value), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(Ids.DepartmentA2, ticket.DepartmentId);
        Assert.Equal(Ids.BranchA2, ticket.BranchId);
        Assert.Equal(Ids.DepartmentA2.Value, result.Value.DepartmentId);
        Assert.Equal(Ids.BranchA2.Value, result.Value.BranchId);
        Assert.Null(result.Value.AssignedAgentId);
        Assert.Null(result.Value.Category);
        Assert.Equal(Ids.TenantA.Value, result.Value.TenantId);
    }

    [Fact]
    public async Task TheEventCarriesTheDepartmentsBeforeAndAfterAndThePreviousAgent()
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open).Assigned(Ids.Agent1);
        Harness h = HarnessFor(ticket);

        await Handler(h).Handle(Command(ticket, Ids.DepartmentA2.Value), Stubs.Ct);

        var published = Assert.IsType<TicketTransferredIntegrationEvent>(Assert.Single(h.Published));
        Assert.Equal(Ids.BranchA1.Value, published.FromBranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.FromDepartmentId);
        Assert.Equal(Ids.BranchA2.Value, published.ToBranchId);
        Assert.Equal(Ids.DepartmentA2.Value, published.ToDepartmentId);
        Assert.Equal(Ids.Agent1, published.PreviousAgentId);
        Assert.Equal(Ids.Actor, published.ActorId);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(ticket.Id.Value, published.TicketId);
        Assert.Equal("T-000001", published.Number);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AnUnidentifiableUserThrowsActorRequired(string? userId)
    {
        Ticket ticket = TicketFixtures.InStatus(TicketStatus.Open);
        var h = new Harness(user: CurrentUsers.WithUserId(Scopes.AgentA1(), userId));
        h.Holding(ticket);
        h.KnowsDepartments(TicketFixtures.Department(Ids.DepartmentA2, branch: Ids.BranchA2));

        await Expect.DomainCode(() => Handler(h).Handle(Command(ticket, Ids.DepartmentA2.Value), Stubs.Ct), ErrorCodes.ActorRequired);

        Assert.Equal(0, h.Saves);
        Assert.Empty(h.Published);
    }
}
