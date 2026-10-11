using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Behaviors;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Tests.Application.Behaviors;

public sealed class AuthorizationBehaviorTests
{
    private static readonly ScopeMembership AgentA1 = Scopes.Member(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1, CrmRoles.Agent);
    private static readonly ScopeMembership AdminA2 = Scopes.Member(ScopeFixtures.DeptA2, ScopeFixtures.BranchA2, CrmRoles.Administrator);

    private static AuthorizationBehavior<TRequest, TResponse> Behavior<TRequest, TResponse>(
        ICurrentUserService user, RecordingLogger<AuthorizationBehavior<TRequest, TResponse>>? logger = null)
        where TRequest : notnull =>
        new(user, logger ?? new RecordingLogger<AuthorizationBehavior<TRequest, TResponse>>());

    private static ICurrentUserService Agent() => CurrentUsers.Agent(Scopes.Agent(AgentA1));

    [Fact]
    public async Task AnAnonymousRequestRunsWithNoUser()
    {
        var behavior = Behavior<AnonymousCommand, Result>(CurrentUsers.Anonymous());
        var next = new NextProbe<Result>(Result.Success());

        var result = await behavior.Handle(new AnonymousCommand(), next.Next, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, next.Calls);
    }

    [Fact]
    public async Task AnUnrestrictedScopeSkipsThePermissionCheck()
    {
        var behavior = Behavior<AdminCommand, Result>(CurrentUsers.Unrestricted());
        var next = new NextProbe<Result>(Result.Success());

        var result = await behavior.Handle(new AdminCommand(), next.Next, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, next.Calls);
    }

    [Fact]
    public async Task NoUserIsUnauthorizedAndTheHandlerNeverRuns()
    {
        var logger = new RecordingLogger<AuthorizationBehavior<ReadQuery, Result<int>>>();
        var behavior = Behavior(CurrentUsers.Anonymous(), logger);
        var next = new NextProbe<Result<int>>(Result.Success(1));

        var result = await behavior.Handle(new ReadQuery(), next.Next, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Authorization.Unauthenticated", result.Error.Code);
        Assert.False(next.WasCalled);
        Assert.True(logger.Contains(LogLevel.Information, "ReadQuery"));
    }

    [Fact]
    public async Task APortalPrincipalIsRefusedOnAStaffOnlyRequest()
    {
        var logger = new RecordingLogger<AuthorizationBehavior<AuthenticatedOnlyQuery, Result<int>>>();
        var portal = CurrentUsers.Portal(Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid), "portal-9");
        var behavior = Behavior(portal, logger);
        var next = new NextProbe<Result<int>>(Result.Success(1));

        var result = await behavior.Handle(new AuthenticatedOnlyQuery(), next.Next, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("Authorization.StaffOnly", result.Error.Code);
        Assert.False(next.WasCalled);
        Assert.True(logger.Contains(LogLevel.Warning, "portal-9"));
    }

    [Fact]
    public async Task StaffPassAnAuthenticatedOnlyRequest()
    {
        var behavior = Behavior<AuthenticatedOnlyQuery, Result<int>>(Agent());
        var next = new NextProbe<Result<int>>(Result.Success(7));

        var result = await behavior.Handle(new AuthenticatedOnlyQuery(), next.Next, TestContext.Current.CancellationToken);

        Assert.Equal(7, result.Value);
        Assert.Equal(1, next.Calls);
    }

    [Fact]
    public async Task APermissionHeldThroughARolePasses()
    {
        var behavior = Behavior<ReadQuery, Result<int>>(Agent());
        var next = new NextProbe<Result<int>>(Result.Success(3));

        var result = await behavior.Handle(new ReadQuery(), next.Next, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Value);
    }

    [Fact]
    public async Task AMissingPermissionIsForbiddenAndNamesThePermission()
    {
        var logger = new RecordingLogger<AuthorizationBehavior<AssignCommand, Result>>();
        var behavior = Behavior(Agent(), logger);
        var next = new NextProbe<Result>(Result.Success());

        var result = await behavior.Handle(new AssignCommand(), next.Next, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("Authorization.PermissionDenied", result.Error.Code);
        Assert.Contains("tickets.assign", result.Error.Description, StringComparison.Ordinal);
        Assert.False(next.WasCalled);
        Assert.True(logger.Contains(LogLevel.Warning, "tickets.assign"));
    }

    [Fact]
    public async Task ASupervisorMayAssign()
    {
        var supervisor = CurrentUsers.Agent(Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1, CrmRoles.Supervisor));
        var behavior = Behavior<AssignCommand, Result>(supervisor);
        var next = new NextProbe<Result>(Result.Success());

        var result = await behavior.Handle(new AssignCommand(), next.Next, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PermissionsAreJudgedOnEffectiveRolesOnly()
    {
        // Agent in A1, Administrator in A2: narrowed to A1 the caller is just an agent.
        var narrowed = CurrentUsers.Agent(Scopes.Narrowed([AgentA1, AdminA2], ScopeFixtures.DeptA1));
        var everywhere = CurrentUsers.Agent(Scopes.Agent(AgentA1, AdminA2));

        var denied = await Behavior<AdminCommand, Result>(narrowed)
            .Handle(new AdminCommand(), new NextProbe<Result>(Result.Success()).Next, TestContext.Current.CancellationToken);
        var allowed = await Behavior<AdminCommand, Result>(everywhere)
            .Handle(new AdminCommand(), new NextProbe<Result>(Result.Success()).Next, TestContext.Current.CancellationToken);

        Assert.Equal("Authorization.PermissionDenied", denied.Error.Code);
        Assert.True(allowed.IsSuccess);
    }

    [Fact]
    public async Task ARejectedActiveScopeHoldsNoPermission()
    {
        var rejected = CurrentUsers.Agent(Scopes.Rejected([AgentA1]));

        var result = await Behavior<ReadQuery, Result<int>>(rejected)
            .Handle(new ReadQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.Equal("Authorization.PermissionDenied", result.Error.Code);
    }

    [Fact]
    public async Task APortalCustomerPassesAPortalPermissionRequest()
    {
        var portal = CurrentUsers.Portal(Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid));
        var behavior = Behavior<PortalTicketsQuery, Result<int>>(portal);

        var result = await behavior.Handle(new PortalTicketsQuery(), new NextProbe<Result<int>>(Result.Success(2)).Next, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Value);
    }

    [Fact]
    public async Task APortalCustomerFailsAStaffPermissionRequest()
    {
        var portal = CurrentUsers.Portal(Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid));
        var behavior = Behavior<ReadQuery, Result<int>>(portal);

        var result = await behavior.Handle(new ReadQuery(), new NextProbe<Result<int>>(Result.Success(2)).Next, TestContext.Current.CancellationToken);

        Assert.Equal("Authorization.PermissionDenied", result.Error.Code);
    }

    [Fact]
    public async Task AStaffUserFailsAPortalPermissionRequest()
    {
        var staff = CurrentUsers.Agent(Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1, CrmRoles.Administrator));
        var behavior = Behavior<PortalTicketsQuery, Result<int>>(staff);

        var result = await behavior.Handle(new PortalTicketsQuery(), new NextProbe<Result<int>>(Result.Success(2)).Next, TestContext.Current.CancellationToken);

        Assert.Equal("Authorization.PermissionDenied", result.Error.Code);
    }

    [Fact]
    public async Task ATypedResponseGetsATypedFailure()
    {
        var behavior = Behavior<ReadQuery, Result<int>>(CurrentUsers.Anonymous());

        var result = await behavior.Handle(new ReadQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.IsType<Result<int>>(result);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public async Task APlainResultResponseGetsAPlainFailure()
    {
        var behavior = Behavior<WriteCommand, Result>(CurrentUsers.Anonymous());

        var result = await behavior.Handle(new WriteCommand(), new NextProbe<Result>(Result.Success()).Next, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Authorization.Unauthenticated", result.Error.Code);
    }

    [Fact]
    public async Task ANullNextDelegateIsRefused()
    {
        var behavior = Behavior<WriteCommand, Result>(Agent());

        await NullArguments.AssertThrowsArgumentNullAsync(
            behavior.Handle, new WriteCommand(), null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NothingIsLoggedWhenTheCallIsAllowed()
    {
        var logger = new RecordingLogger<AuthorizationBehavior<ReadQuery, Result<int>>>();
        var behavior = Behavior(Agent(), logger);

        await behavior.Handle(new ReadQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
    }
}
