namespace Crm.BuildingBlocks.Application.Caching;

/// <summary>
/// A query whose successful result CachingBehavior caches. Must return <c>Result&lt;T&gt;</c>. Keys and tags follow
/// "{module}:{aggregate-plural}:{id}" (e.g. "sample:samples:{id}"), built by the module's <c>&lt;Module&gt;CacheKeys</c>
/// class; the behaviour prefixes the caller's tenant/department/branch scope.
/// </summary>
public interface ICacheableQuery
{
    string CacheKey { get; }

    IReadOnlyCollection<string> CacheTags { get; }

    /// <summary>null uses <c>Caching:DefaultExpiration</c>.</summary>
    TimeSpan? CacheDuration => null;

    /// <summary>true when the result depends on who asks, not only on their scope.</summary>
    bool VaryByUser => false;
}

/// <summary>
/// A command whose successful commit invalidates these tags. TransactionBehavior invalidates AFTER commit, never
/// inside the transaction - otherwise a concurrent read could re-cache the old row before this one commits.
/// </summary>
public interface ICacheInvalidatingCommand
{
    IReadOnlyCollection<string> CacheTagsToInvalidate { get; }
}
