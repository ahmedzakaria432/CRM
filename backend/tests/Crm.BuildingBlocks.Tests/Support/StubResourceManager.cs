using System.Globalization;
using System.Resources;

namespace Crm.BuildingBlocks.Tests.Support;

/// <summary>A resource bundle held in a dictionary, for lookup-order tests that must not depend on a .resx.</summary>
public sealed class StubResourceManager(IReadOnlyDictionary<string, string> values) : ResourceManager
{
    public override string? GetString(string name, CultureInfo? culture) =>
        values.TryGetValue(name, out string? value) ? value : null;
}
