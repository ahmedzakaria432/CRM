using System.Text.Json;
using Crm.BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// <see cref="ICacheService"/> over IDistributedCache (Redis in every configured environment). Tags are version
/// tokens: an entry stores the token of each of its tags at the time its data was read, and is a miss once any
/// tag's current token differs. Invalidation writes a fresh token - no read-modify-write, so no race.
/// Every cache call is guarded: a cache outage never fails a request.
/// </summary>
public sealed partial class DistributedCacheService(IDistributedCache cache, ILogger<DistributedCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DistributedCacheEntryOptions TagOptions = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) };

    // Once a cache call fails, the cache is bypassed for this long. Without it every request pays the client's
    // full timeout on every cache call while Redis is down - verified by hand: ~12 s per cached GET.
    private const long BypassMilliseconds = 30_000;
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(2);
    private long _bypassUntil;

    private bool IsBypassed => Environment.TickCount64 < Interlocked.Read(ref _bypassUntil);

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CacheEntrySettings settings,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(settings);

        if (IsBypassed)
        {
            return await factory(cancellationToken);
        }

        CacheEnvelope<T>? envelope = await TryAsync("get", key, () => ReadEntryAsync<T>(key, cancellationToken));

        if (envelope is not null
            && await TryAsync("get-tags", key, () => ReadTagVersionsAsync(envelope.Tags.Keys, cancellationToken)) is { } current
            && envelope.Tags.All(tag => current.TryGetValue(tag.Key, out string? version) && version == tag.Value))
        {
            LogCacheHit(key);
            return envelope.Payload;
        }

        // Read tag versions BEFORE the factory: if a command invalidates while the factory reads stale data,
        // the stored entry carries the old token and is unreachable, instead of being cached as fresh.
        Dictionary<string, string>? tagVersions =
            await TryAsync("get-tags", key, () => ReadOrCreateTagVersionsAsync(settings.Tags, cancellationToken));

        LogCacheMiss(key);
        T? value = await factory(cancellationToken);

        if (value is not null && tagVersions is not null)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new CacheEnvelope<T>(tagVersions, value), JsonOptions);
            await TryAsync("set", key, async () =>
            {
                await cache.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = settings.Expiration }, cancellationToken);
                return true;
            });
        }

        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        await TryAsync("remove", key, async () =>
        {
            await cache.RemoveAsync(key, cancellationToken);
            return true;
        });

    public async Task InvalidateTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);

        foreach (string tag in tags)
        {
            if (IsBypassed)
            {
                // Error, not Warning: entries under this tag stay stale until their TTL.
                LogInvalidationSkipped(tag);
                continue;
            }

            try
            {
                await cache.SetStringAsync(TagKey(tag), NewVersion(), TagOptions, cancellationToken).WaitAsync(OperationTimeout, cancellationToken);
                LogTagInvalidated(tag);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                StartBypass();
                LogInvalidationFailed(tag, ex);
            }
        }
    }

    private async Task<CacheEnvelope<T>?> ReadEntryAsync<T>(string key, CancellationToken cancellationToken)
    {
        byte[]? bytes = await cache.GetAsync(key, cancellationToken);
        return bytes is null ? null : JsonSerializer.Deserialize<CacheEnvelope<T>>(bytes, JsonOptions);
    }

    private async Task<Dictionary<string, string>> ReadTagVersionsAsync(IEnumerable<string> tags, CancellationToken cancellationToken)
    {
        var versions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string tag in tags)
        {
            if (await cache.GetStringAsync(TagKey(tag), cancellationToken) is { } version)
            {
                versions[tag] = version;
            }
        }

        return versions;
    }

    private async Task<Dictionary<string, string>> ReadOrCreateTagVersionsAsync(IEnumerable<string> tags, CancellationToken cancellationToken)
    {
        var versions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string tag in tags)
        {
            string? version = await cache.GetStringAsync(TagKey(tag), cancellationToken);
            if (version is null)
            {
                version = NewVersion();
                await cache.SetStringAsync(TagKey(tag), version, TagOptions, cancellationToken);
            }

            versions[tag] = version;
        }

        return versions;
    }

    private async Task<TResult?> TryAsync<TResult>(string operation, string key, Func<Task<TResult>> action)
    {
        if (IsBypassed)
        {
            return default;
        }

        try
        {
            // The Redis client's reconnect wait ignores asyncTimeout (~12 s per call, measured) - this cap is
            // what bounds the first request of an outage. The abandoned call finishes harmlessly in the background.
            return await action().WaitAsync(OperationTimeout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StartBypass();
            LogCacheUnavailable(operation, key, BypassMilliseconds / 1000, ex);
            return default;
        }
    }

    private void StartBypass() => Interlocked.Exchange(ref _bypassUntil, Environment.TickCount64 + BypassMilliseconds);

    private static string TagKey(string tag) => $"tag:{tag}";

    private static string NewVersion() => Guid.NewGuid().ToString("N");

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache hit {CacheKey}")]
    private partial void LogCacheHit(string cacheKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache miss {CacheKey}")]
    private partial void LogCacheMiss(string cacheKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache tag {CacheTag} invalidated")]
    private partial void LogTagInvalidated(string cacheTag);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache unavailable during {Operation} for {CacheKey}; bypassing the cache for {BypassSeconds} s")]
    private partial void LogCacheUnavailable(string operation, string cacheKey, long bypassSeconds, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cache tag {CacheTag} not invalidated: the cache is unavailable; its entries stay stale until they expire")]
    private partial void LogInvalidationSkipped(string cacheTag);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cache tag {CacheTag} could not be invalidated; its entries stay stale until they expire")]
    private partial void LogInvalidationFailed(string cacheTag, Exception exception);
}

internal sealed record CacheEnvelope<T>(Dictionary<string, string> Tags, T Payload);
