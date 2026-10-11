using Crm.BuildingBlocks.Application.Caching;

namespace Crm.BuildingBlocks.Tests.Support;

/// <summary>A cache that really stores what it is given, so hit/miss flows can be observed end to end. Tags are
/// recorded but not enforced - tag invalidation is covered against the real service.</summary>
public sealed class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, object> _entries = [];

    public List<string> Keys { get; } = [];

    public List<CacheEntrySettings> Settings { get; } = [];

    public List<string> Removed { get; } = [];

    public List<string> InvalidatedTags { get; } = [];

    public int StoredCount => _entries.Count;

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CacheEntrySettings settings,
        CancellationToken cancellationToken = default)
        where T : class
    {
        Keys.Add(key);
        Settings.Add(settings);

        if (_entries.TryGetValue(key, out object? cached))
        {
            return (T)cached;
        }

        T? value = await factory(cancellationToken);
        if (value is not null)
        {
            _entries[key] = value;
        }

        return value;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        Removed.Add(key);
        _entries.Remove(key);
        return Task.CompletedTask;
    }

    public Task InvalidateTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
    {
        InvalidatedTags.AddRange(tags);
        return Task.CompletedTask;
    }
}
