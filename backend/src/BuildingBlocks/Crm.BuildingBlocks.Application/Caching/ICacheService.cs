namespace Crm.BuildingBlocks.Application.Caching;

/// <summary>
/// Distributed cache with tag invalidation. The Redis implementation lives in Crm.BuildingBlocks.Infrastructure.
/// A cache outage never fails a caller: reads fall through to the factory, writes are skipped.
/// </summary>
public interface ICacheService
{
    /// <summary>Returns the cached value, or runs <paramref name="factory"/> and caches its result. A null from the
    /// factory is returned and NOT cached (a failed Result must never be cached). Tag versions are read BEFORE the
    /// factory runs, so an invalidation that lands while the factory is reading stale data makes the entry
    /// unreachable instead of caching it as fresh.</summary>
    Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CacheEntrySettings settings,
        CancellationToken cancellationToken = default)
        where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Makes every entry carrying any of these tags unreachable, in every scope, on every instance.</summary>
    Task InvalidateTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default);
}

public sealed record CacheEntrySettings(TimeSpan Expiration, IReadOnlyCollection<string> Tags);

/// <summary>Bound from the "Caching" configuration section.</summary>
public sealed record CachingSettings(TimeSpan DefaultExpiration)
{
    public static readonly CachingSettings Default = new(TimeSpan.FromMinutes(5));
}
