using Crm.BuildingBlocks.Application.Behaviors;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Tests.Application.Behaviors;

public sealed class OrganisationScopeBehaviorTests
{
    private static OrganisationScopeBehavior<TRequest, TResponse> Behavior<TRequest, TResponse>(
        RecordingLogger<OrganisationScopeBehavior<TRequest, TResponse>>? logger = null)
        where TRequest : notnull =>
        new(CurrentUsers.Agent(Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1)),
            logger ?? new RecordingLogger<OrganisationScopeBehavior<TRequest, TResponse>>());

    [Fact]
    public async Task AResponseWithNoViolationPassesThrough()
    {
        var behavior = Behavior<WriteCommand, Result>();
        var response = Result.Success();

        var result = await behavior.Handle(new WriteCommand(), new NextProbe<Result>(response).Next, TestContext.Current.CancellationToken);

        Assert.Same(response, result);
    }

    [Fact]
    public async Task AScopeViolationBecomesAForbiddenResultAndIsLogged()
    {
        var logger = new RecordingLogger<OrganisationScopeBehavior<WriteCommand, Result>>();
        var behavior = Behavior(logger);
        var next = NextProbe.Throwing<Result>(new OrganisationScopeViolationException("Ticket", "Write"));

        var result = await behavior.Handle(new WriteCommand(), next.Next, TestContext.Current.CancellationToken);

        Assert.Equal(Error.Forbidden("Organisation.OutOfScope", "The record belongs to a department outside your scope."), result.Error);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Write", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Ticket", entry.Message, StringComparison.Ordinal);
        Assert.Contains("WriteCommand", entry.Message, StringComparison.Ordinal);
        Assert.Contains(CurrentUsers.DefaultUserId, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AScopeViolationOnATypedResponseBecomesATypedFailure()
    {
        var behavior = Behavior<ReadQuery, Result<int>>();
        var next = NextProbe.Throwing<Result<int>>(new OrganisationScopeViolationException("Ticket", "Read"));

        var result = await behavior.Handle(new ReadQuery(), next.Next, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Organisation.OutOfScope", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task ANonResultResponseLetsTheViolationPropagate()
    {
        var behavior = Behavior<NonResultRequest, string>();
        var violation = new OrganisationScopeViolationException("Ticket", "Read");
        var next = NextProbe.Throwing<string>(violation);

        var thrown = await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => behavior.Handle(new NonResultRequest(), next.Next, TestContext.Current.CancellationToken));

        Assert.Same(violation, thrown);
    }

    [Fact]
    public async Task OtherExceptionsPropagate()
    {
        var behavior = Behavior<WriteCommand, Result>();
        var next = NextProbe.Throwing<Result>(new InvalidOperationException("boom"));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(new WriteCommand(), next.Next, TestContext.Current.CancellationToken));

        Assert.Equal("boom", thrown.Message);
    }

    [Fact]
    public async Task AnAnonymousCallerIsLoggedAsSystemWithADashForNoDepartments()
    {
        var logger = new RecordingLogger<OrganisationScopeBehavior<WriteCommand, Result>>();
        var behavior = new OrganisationScopeBehavior<WriteCommand, Result>(CurrentUsers.Anonymous(), logger);
        var next = NextProbe.Throwing<Result>(new OrganisationScopeViolationException("Ticket", "Write"));

        await behavior.Handle(new WriteCommand(), next.Next, TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("system", entry.Message, StringComparison.Ordinal);
        Assert.Contains("(departments -)", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANullNextDelegateIsRefused()
    {
        var behavior = Behavior<WriteCommand, Result>();

        await NullArguments.AssertThrowsArgumentNullAsync(
            behavior.Handle, new WriteCommand(), null, TestContext.Current.CancellationToken);
    }
}
