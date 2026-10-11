using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Infrastructure.Caching;
using Crm.BuildingBlocks.Tests.Support;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Caching;

public sealed record CachedThing(string Name);

public sealed class DistributedCacheServiceTests
{
    private static readonly CacheEntrySettings Settings = new(TimeSpan.FromMinutes(2), ["t"]);

    private sealed class Counter(CachedThing? value)
    {
        public int Calls { get; private set; }

        public Task<CachedThing?> Run(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(value);
        }
    }

    private static (DistributedCacheService Service, ControllableDistributedCache Cache, RecordingLogger<DistributedCacheService> Logger) Create()
    {
        var cache = new ControllableDistributedCache();
        var logger = new RecordingLogger<DistributedCacheService>();
        return (new DistributedCacheService(cache, logger), cache, logger);
    }

    private static Task<CachedThing?> Get(DistributedCacheService service, string key, Counter counter, CacheEntrySettings? settings = null) =>
        service.GetOrCreateAsync(key, counter.Run, settings ?? Settings, TestContext.Current.CancellationToken);

    [Fact]
    public async Task AMissRunsTheFactoryOnceAndStoresTheValue()
    {
        var (service, cache, _) = Create();
        var counter = new Counter(new CachedThing("a"));

        var value = await Get(service, "k", counter);

        Assert.Equal(new CachedThing("a"), value);
        Assert.Equal(1, counter.Calls);
        Assert.True(cache.SetOptions.ContainsKey("k"));
    }

    [Fact]
    public async Task ASecondCallIsAHitAndSkipsTheFactory()
    {
        var (service, _, _) = Create();
        var counter = new Counter(new CachedThing("a"));

        await Get(service, "k", counter);
        var second = await Get(service, "k", counter);

        Assert.Equal(new CachedThing("a"), second);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task ANullFactoryResultIsReturnedButNeverStored()
    {
        var (service, _, _) = Create();
        var counter = new Counter(null);

        var first = await Get(service, "k", counter);
        var second = await Get(service, "k", counter);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, counter.Calls);
    }

    [Fact]
    public async Task InvalidatingATagMakesTheEntryStale()
    {
        var (service, _, _) = Create();
        var counter = new Counter(new CachedThing("a"));
        await Get(service, "k", counter);

        await service.InvalidateTagsAsync(["t"], TestContext.Current.CancellationToken);
        await Get(service, "k", counter);

        Assert.Equal(2, counter.Calls);
    }

    [Fact]
    public async Task InvalidatingAnUnrelatedTagKeepsTheHit()
    {
        var (service, _, _) = Create();
        var counter = new Counter(new CachedThing("a"));
        await Get(service, "k", counter);

        await service.InvalidateTagsAsync(["unrelated"], TestContext.Current.CancellationToken);
        await Get(service, "k", counter);

        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task AnEntryWithSeveralTagsIsStaleWhenAnyOneIsInvalidated()
    {
        var (service, _, _) = Create();
        var counter = new Counter(new CachedThing("a"));
        var settings = new CacheEntrySettings(TimeSpan.FromMinutes(2), ["one", "two", "three"]);
        await Get(service, "k", counter, settings);

        await service.InvalidateTagsAsync(["two"], TestContext.Current.CancellationToken);
        await Get(service, "k", counter, settings);

        Assert.Equal(2, counter.Calls);
    }

    [Fact]
    public async Task AnInvalidationReachesEveryKeyCarryingTheTag()
    {
        var (service, _, _) = Create();
        var counter = new Counter(new CachedThing("a"));
        await Get(service, "k1", counter);
        await Get(service, "k2", counter);

        await service.InvalidateTagsAsync(["t"], TestContext.Current.CancellationToken);
        await Get(service, "k1", counter);
        await Get(service, "k2", counter);

        Assert.Equal(4, counter.Calls);
    }

    [Fact]
    public async Task RemoveAsyncForcesAMiss()
    {
        var (service, _, _) = Create();
        var counter = new Counter(new CachedThing("a"));
        await Get(service, "k", counter);

        await service.RemoveAsync("k", TestContext.Current.CancellationToken);
        await Get(service, "k", counter);

        Assert.Equal(2, counter.Calls);
    }

    [Fact]
    public async Task TheEntryIsStoredWithTheRequestedExpiration()
    {
        var (service, cache, _) = Create();

        await Get(service, "k", new Counter(new CachedThing("a")));

        Assert.Equal(TimeSpan.FromMinutes(2), cache.SetOptions["k"].AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task TagVersionsAreStoredForThirtyDays()
    {
        var (service, cache, _) = Create();

        await Get(service, "k", new Counter(new CachedThing("a")));

        Assert.Equal(TimeSpan.FromDays(30), cache.SetOptions["tag:t"].AbsoluteExpirationRelativeToNow);
    }

    // --- outage degradation -------------------------------------------------------------------------------

    [Fact]
    public async Task AFailingReadDegradesToTheFactoryAndLogsAWarning()
    {
        var (service, cache, logger) = Create();
        cache.ThrowOnGet = true;
        var counter = new Counter(new CachedThing("a"));

        var value = await Get(service, "k", counter);

        Assert.Equal(new CachedThing("a"), value);
        Assert.Equal(1, counter.Calls);
        Assert.True(logger.Contains(LogLevel.Warning, "Cache unavailable during get for k"));
    }

    [Fact]
    public async Task AfterAFailureTheNextCallDoesNotTouchTheCache()
    {
        var (service, cache, _) = Create();
        cache.ThrowOnGet = true;
        var counter = new Counter(new CachedThing("a"));
        await Get(service, "k", counter);
        var callsAfterFailure = cache.TotalCalls;

        var value = await Get(service, "k", counter);

        Assert.Equal(new CachedThing("a"), value);
        Assert.Equal(2, counter.Calls);
        Assert.Equal(callsAfterFailure, cache.TotalCalls);
    }

    [Fact]
    public async Task AFailingWriteDegradesTheSameWay()
    {
        var (service, cache, logger) = Create();
        cache.ThrowOnEntrySet = true;
        var counter = new Counter(new CachedThing("a"));

        var first = await Get(service, "k", counter);
        var callsAfterFailure = cache.TotalCalls;
        var second = await Get(service, "k", counter);

        Assert.Equal(new CachedThing("a"), first);
        Assert.Equal(new CachedThing("a"), second);
        Assert.Equal(2, counter.Calls);
        Assert.Equal(callsAfterFailure, cache.TotalCalls);
        Assert.True(logger.Contains(LogLevel.Warning, "Cache unavailable during set for k"));
    }

    [Fact]
    public async Task InvalidationAgainstAFailingCacheNeverThrowsAndLogsAnError()
    {
        var (service, cache, logger) = Create();
        cache.ThrowOnAnySet = true;

        await service.InvalidateTagsAsync(["one"], TestContext.Current.CancellationToken);

        Assert.Equal(1, cache.SetCalls);
        Assert.True(logger.Contains(LogLevel.Error, "Cache tag one could not be invalidated"));
    }

    [Fact]
    public async Task LaterInvalidationsAreSkippedWhileTheCacheIsBypassed()
    {
        var (service, cache, logger) = Create();
        cache.ThrowOnAnySet = true;

        await service.InvalidateTagsAsync(["one", "two"], TestContext.Current.CancellationToken);

        Assert.Equal(1, cache.SetCalls);
        Assert.True(logger.Contains(LogLevel.Error, "Cache tag two not invalidated: the cache is unavailable"));
    }

    [Fact]
    public async Task ARemoveAgainstAFailingCacheDoesNotThrow()
    {
        var (service, cache, _) = Create();
        cache.ThrowOnGet = true;
        await Get(service, "k", new Counter(new CachedThing("a")));

        await service.RemoveAsync("k", TestContext.Current.CancellationToken);

        Assert.Equal(0, cache.RemoveCalls);
    }

    [Fact]
    public async Task ACancelledTokenIsNotSwallowed()
    {
        var (service, _, _) = Create();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetOrCreateAsync("k", new Counter(new CachedThing("a")).Run, Settings, source.Token));
    }

    [Fact]
    public async Task ACancelledInvalidationIsNotSwallowed()
    {
        var (service, _, _) = Create();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InvalidateTagsAsync(["t"], source.Token));
    }

    // --- guards -------------------------------------------------------------------------------------------

    [Fact]
    public async Task NullArgumentsAreRefused()
    {
        var (service, _, _) = Create();
        var counter = new Counter(new CachedThing("a"));
        Func<string, Func<CancellationToken, Task<CachedThing?>>, CacheEntrySettings, CancellationToken, Task<CachedThing?>> get =
            service.GetOrCreateAsync;
        Func<IReadOnlyCollection<string>, CancellationToken, Task> invalidate = service.InvalidateTagsAsync;

        await NullArguments.AssertThrowsArgumentNullAsync(get, "k", null, Settings, CancellationToken.None);
        Func<CancellationToken, Task<CachedThing?>> factory = counter.Run;
        await NullArguments.AssertThrowsArgumentNullAsync(get, "k", factory, null, CancellationToken.None);
        await NullArguments.AssertThrowsArgumentNullAsync(invalidate, null, CancellationToken.None);
    }
}
