// CANONICAL SLICE - every module that caches keeps its keys and tags in one class like this one.
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

namespace Crm.Modules.Sample.Application;

/// <summary>Cache keys and tags, "{module}:{aggregate-plural}:{id}". One string serves as both the key of the
/// cached query and the tag its commands invalidate.</summary>
public static class SampleCacheKeys
{
    public static string Sample(Guid id) => $"sample:samples:{id}";
}
