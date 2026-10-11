using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Crm.BuildingBlocks.Tests.Support;

/// <summary>A real in-memory <see cref="IDistributedCache"/> that can be told to fail, and that records what it was
/// asked to do. A hand-written double because the service under test reaches the cache through extension methods
/// (<c>GetStringAsync</c>/<c>SetStringAsync</c>) that an NSubstitute substitute cannot intercept.</summary>
public sealed class ControllableDistributedCache : IDistributedCache
{
    private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));

    public bool ThrowOnGet { get; set; }

    public bool ThrowOnEntrySet { get; set; }

    public bool ThrowOnAnySet { get; set; }

    public int GetCalls { get; private set; }

    public int SetCalls { get; private set; }

    public int RemoveCalls { get; private set; }

    public int TotalCalls => GetCalls + SetCalls + RemoveCalls;

    public Dictionary<string, DistributedCacheEntryOptions> SetOptions { get; } = [];

    public byte[]? Get(string key) => _inner.Get(key);

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        GetCalls++;

        if (ThrowOnGet)
        {
            throw new InvalidOperationException("cache down (get)");
        }

        return _inner.GetAsync(key, token);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _inner.Set(key, value, options);

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        SetCalls++;

        if (ThrowOnAnySet || (ThrowOnEntrySet && !key.StartsWith("tag:", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("cache down (set)");
        }

        SetOptions[key] = options;
        return _inner.SetAsync(key, value, options, token);
    }

    public void Refresh(string key) => _inner.Refresh(key);

    public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);

    public void Remove(string key) => _inner.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        RemoveCalls++;
        return _inner.RemoveAsync(key, token);
    }
}
