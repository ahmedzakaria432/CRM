using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Behaviors;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.BuildingBlocks.Tests.Application.Behaviors;

public sealed class CachingBehaviorTests
{
    private static readonly CachingSettings Settings = new(TimeSpan.FromMinutes(7));

    private static ICurrentUserService AgentA1() =>
        CurrentUsers.Agent(Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1), "user-42");

    private static string KeyFor(ICurrentUserService user, string cacheKey, bool byUser = false)
    {
        var key = $"t={user.TenantId ?? "-"}|d={user.DepartmentId ?? "-"}|b={user.BranchId ?? "-"}";
        return (byUser ? key + $"|u={user.UserIdOrSystem}" : key) + ":" + cacheKey;
    }

    // --- passthrough --------------------------------------------------------------------------------------

    [Fact]
    public async Task ANonCacheableRequestRunsNextAndLeavesTheCacheUntouched()
    {
        var cache = new InMemoryCacheService();
        var behavior = new CachingBehavior<ReadQuery, Result<int>>(cache, AgentA1(), Settings);
        var next = new NextProbe<Result<int>>(Result.Success(1));

        await behavior.Handle(new ReadQuery(), next.Next, TestContext.Current.CancellationToken);
        await behavior.Handle(new ReadQuery(), next.Next, TestContext.Current.CancellationToken);

        Assert.Equal(2, next.Calls);
        Assert.Empty(cache.Keys);
    }

    [Fact]
    public async Task ACacheableRequestWithANonResultResponseFailsNamingBothTypes()
    {
        var behavior = new CachingBehavior<CacheableNonResultQuery, string>(new InMemoryCacheService(), AgentA1(), Settings);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(new CacheableNonResultQuery(), new NextProbe<string>("x").Next, TestContext.Current.CancellationToken));

        Assert.Contains(nameof(CacheableNonResultQuery), exception.Message, StringComparison.Ordinal);
        Assert.Contains("String", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANonCacheableRequestWithANonResultResponseStillPassesThrough()
    {
        var behavior = new CachingBehavior<NonResultRequest, string>(new InMemoryCacheService(), AgentA1(), Settings);

        var response = await behavior.Handle(new NonResultRequest(), new NextProbe<string>("fine").Next, TestContext.Current.CancellationToken);

        Assert.Equal("fine", response);
    }

    [Fact]
    public async Task ANullNextDelegateIsRefused()
    {
        var behavior = new CachingBehavior<ReadQuery, Result<int>>(new InMemoryCacheService(), AgentA1(), Settings);

        await NullArguments.AssertThrowsArgumentNullAsync(
            behavior.Handle, new ReadQuery(), null, TestContext.Current.CancellationToken);
    }

    // --- miss and hit -------------------------------------------------------------------------------------

    [Fact]
    public async Task AMissRunsNextStoresTheValueAndReturnsASuccess()
    {
        var cache = new InMemoryCacheService();
        var behavior = new CachingBehavior<CacheableQuery, Result<int>>(cache, AgentA1(), Settings);
        var next = new NextProbe<Result<int>>(Result.Success(5));

        var result = await behavior.Handle(new CacheableQuery(), next.Next, TestContext.Current.CancellationToken);

        Assert.Equal(5, result.Value);
        Assert.Equal(1, next.Calls);
        Assert.Equal(1, cache.StoredCount);
    }

    [Fact]
    public async Task AHitDoesNotRunNextAndReturnsTheCachedValue()
    {
        var cache = new InMemoryCacheService();
        var behavior = new CachingBehavior<CacheableQuery, Result<int>>(cache, AgentA1(), Settings);
        var first = new NextProbe<Result<int>>(Result.Success(5));
        var second = new NextProbe<Result<int>>(Result.Success(99));

        await behavior.Handle(new CacheableQuery(), first.Next, TestContext.Current.CancellationToken);
        var result = await behavior.Handle(new CacheableQuery(), second.Next, TestContext.Current.CancellationToken);

        Assert.Equal(5, result.Value);
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public async Task AFailureIsNeverCachedAndTheOriginalFailureIsReturned()
    {
        var cache = new InMemoryCacheService();
        var behavior = new CachingBehavior<CacheableQuery, Result<int>>(cache, AgentA1(), Settings);
        var failure = Result.Failure<int>(Error.NotFound("Thing.NotFound", "gone"));
        var failing = new NextProbe<Result<int>>(failure);
        var succeeding = new NextProbe<Result<int>>(Result.Success(3));

        var first = await behavior.Handle(new CacheableQuery(), failing.Next, TestContext.Current.CancellationToken);
        Assert.Equal(0, cache.StoredCount);
        var second = await behavior.Handle(new CacheableQuery(), succeeding.Next, TestContext.Current.CancellationToken);

        Assert.Same(failure, first);
        Assert.Equal(1, succeeding.Calls);
        Assert.Equal(3, second.Value);
    }

    [Fact]
    public async Task ACacheThatReturnsNothingAndRecordedNoFailureFailsLoudly()
    {
        var cache = Substitute.For<ICacheService>();
        cache.GetOrCreateAsync<CachedValue<int>>(
                Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<CachedValue<int>?>>>(), Arg.Any<CacheEntrySettings>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CachedValue<int>?>(null));
        var behavior = new CachingBehavior<CacheableQuery, Result<int>>(cache, AgentA1(), Settings);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(new CacheableQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AHandlerExceptionPropagates()
    {
        var behavior = new CachingBehavior<CacheableQuery, Result<int>>(new InMemoryCacheService(), AgentA1(), Settings);
        var boom = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(new CacheableQuery(), NextProbe.Throwing<Result<int>>(boom).Next, TestContext.Current.CancellationToken));

        Assert.Same(boom, thrown);
    }

    // --- keys ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task TheKeyIsPrefixedWithTheCallersTenantDepartmentAndBranch()
    {
        var cache = new InMemoryCacheService();
        var user = AgentA1();
        var behavior = new CachingBehavior<CacheableQuery, Result<int>>(cache, user, Settings);

        await behavior.Handle(new CacheableQuery("item:9"), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.Equal(KeyFor(user, "item:9"), Assert.Single(cache.Keys));
        Assert.StartsWith($"t={ScopeFixtures.TenantAGuid}|d={ScopeFixtures.DeptA1Guid}|b={ScopeFixtures.BranchA1Guid}:", cache.Keys[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingScopeSegmentsAreRenderedAsADash()
    {
        var cache = new InMemoryCacheService();
        var behavior = new CachingBehavior<CacheableQuery, Result<int>>(cache, CurrentUsers.Anonymous(), Settings);

        await behavior.Handle(new CacheableQuery("item:1"), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.Equal("t=-|d=-|b=-:item:1", Assert.Single(cache.Keys));
    }

    [Fact]
    public async Task VaryByUserAppendsTheUserId()
    {
        var cache = new InMemoryCacheService();
        var user = AgentA1();
        var behavior = new CachingBehavior<CacheableUserQuery, Result<int>>(cache, user, Settings);

        await behavior.Handle(new CacheableUserQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.Equal(KeyFor(user, "me", byUser: true), Assert.Single(cache.Keys));
        Assert.Contains("|u=user-42:", cache.Keys[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutVaryByUserTheUserIsNotInTheKey()
    {
        var cache = new InMemoryCacheService();
        var behavior = new CachingBehavior<CacheableQuery, Result<int>>(cache, AgentA1(), Settings);

        await behavior.Handle(new CacheableQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("|u=", cache.Keys[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoTenantsNeverShareACacheEntry()
    {
        var cache = new InMemoryCacheService();
        var tenantA = CurrentUsers.Agent(Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1));
        var tenantB = CurrentUsers.Agent(Scopes.AgentOfTenant(
            ScopeFixtures.TenantB, Scopes.Member(ScopeFixtures.DeptB1, ScopeFixtures.BranchB1)));
        var nextA = new NextProbe<Result<int>>(Result.Success(1));
        var nextB = new NextProbe<Result<int>>(Result.Success(2));

        var a = await new CachingBehavior<CacheableQuery, Result<int>>(cache, tenantA, Settings)
            .Handle(new CacheableQuery(), nextA.Next, TestContext.Current.CancellationToken);
        var b = await new CachingBehavior<CacheableQuery, Result<int>>(cache, tenantB, Settings)
            .Handle(new CacheableQuery(), nextB.Next, TestContext.Current.CancellationToken);

        Assert.Equal(1, a.Value);
        Assert.Equal(2, b.Value);
        Assert.NotEqual(cache.Keys[0], cache.Keys[1]);
        Assert.Equal(1, nextB.Calls);
    }

    // --- settings -----------------------------------------------------------------------------------------

    [Fact]
    public async Task TheQueryDurationOverridesTheDefaultExpiration()
    {
        var cache = new InMemoryCacheService();
        var behavior = new CachingBehavior<CacheableTimedQuery, Result<int>>(cache, AgentA1(), Settings);

        await behavior.Handle(new CacheableTimedQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(42), Assert.Single(cache.Settings).Expiration);
    }

    [Fact]
    public async Task WithoutADurationTheDefaultExpirationApplies()
    {
        var cache = new InMemoryCacheService();
        var behavior = new CachingBehavior<CacheableQuery, Result<int>>(cache, AgentA1(), Settings);

        await behavior.Handle(new CacheableQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(7), Assert.Single(cache.Settings).Expiration);
    }

    [Fact]
    public async Task TheQueryTagsArePassedToTheCache()
    {
        var cache = new InMemoryCacheService();
        var behavior = new CachingBehavior<CacheableTimedQuery, Result<int>>(cache, AgentA1(), Settings);

        await behavior.Handle(new CacheableTimedQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b"], Assert.Single(cache.Settings).Tags);
    }

    [Fact]
    public async Task ACacheableQueryReceivesTheCancellationTokenInItsFactory()
    {
        using var source = new CancellationTokenSource();
        var cache = Substitute.For<ICacheService>();
        var behavior = new CachingBehavior<CacheableQuery, Result<int>>(cache, AgentA1(), Settings);
        cache.GetOrCreateAsync<CachedValue<int>>(
                Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<CachedValue<int>?>>>(), Arg.Any<CacheEntrySettings>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CachedValue<int>?>(new CachedValue<int>(4)));

        var result = await behavior.Handle(new CacheableQuery(), new NextProbe<Result<int>>(Result.Success(1)).Next, source.Token);

        Assert.Equal(4, result.Value);
        await cache.Received(1).GetOrCreateAsync<CachedValue<int>>(
            Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<CachedValue<int>?>>>(), Arg.Any<CacheEntrySettings>(), source.Token);
    }
}
